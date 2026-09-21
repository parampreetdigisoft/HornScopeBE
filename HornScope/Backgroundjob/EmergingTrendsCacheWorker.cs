using Microsoft.Extensions.Configuration;
using HornScope.IServices;

namespace HornScope.Backgroundjob
{
    /// <summary>
    /// Refreshes emerging trends every 10 minutes. Failed refreshes keep the last good in-memory and disk snapshot.
    /// Failed refreshes keep serving the last saved JSON file.
    /// </summary>
    public class EmergingTrendsCacheWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmergingTrendsCacheWorker> _logger;

        public EmergingTrendsCacheWorker(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<EmergingTrendsCacheWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var countryCount = _configuration.GetValue("EmergingTrendsCache:CountryCount", 8);
            var refreshInterval = TimeSpan.FromMinutes(
                _configuration.GetValue("EmergingTrendsCache:RefreshIntervalMinutes", 10));
            var retryDelay = TimeSpan.FromSeconds(
                _configuration.GetValue("EmergingTrendsCache:RetryDelaySeconds", 60));

            var hydrated = HydrateFromDisk(countryCount);
            if (!hydrated)
            {
                await RefreshUntilCachedAsync(countryCount, retryDelay, stoppingToken);
            }
            else
            {
                await TryRefreshAsync(countryCount, stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(refreshInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                await TryRefreshAsync(countryCount, stoppingToken);
            }
        }


        private bool HydrateFromDisk(int countryCount)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var publicService = scope.ServiceProvider.GetRequiredService<IPublicService>();
                if (publicService.HydrateEmergingTrendsCacheFromDisk(countryCount))
                {
                    _logger.LogInformation(
                        "Emerging trends cache hydrated from disk (countryCount={CountryCount})",
                        countryCount);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Emerging trends disk hydrate failed (countryCount={CountryCount})",
                    countryCount);
            }

            return false;
        }
        private async Task RefreshUntilCachedAsync(
            int countryCount,
            TimeSpan retryDelay,
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await TryRefreshAsync(countryCount, stoppingToken))
                {
                    return;
                }

                try
                {
                    await Task.Delay(retryDelay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task<bool> TryRefreshAsync(int countryCount, CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var publicService = scope.ServiceProvider.GetRequiredService<IPublicService>();

                var preserved = await publicService.RefreshEmergingTrendsCacheAsync(
                    countryCount,
                    stoppingToken);

                if (!preserved)
                {
                    _logger.LogWarning(
                        "Emerging trends cache refresh produced no usable data and no prior snapshot was available (countryCount={CountryCount})",
                        countryCount);
                }

                return preserved;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Emerging trends cache refresh failed (countryCount={CountryCount})",
                    countryCount);
                return false;
            }
        }
    }
}
