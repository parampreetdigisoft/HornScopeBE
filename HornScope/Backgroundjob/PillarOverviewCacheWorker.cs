using HornScope.IServices;

namespace HornScope.Backgroundjob
{
    /// <summary>
    /// Refreshes the pillar overview after the cached snapshot is 7 days old.
    /// A failed refresh keeps the last saved JSON file.
    /// </summary>
    public class PillarOverviewCacheWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PillarOverviewCacheWorker> _logger;

        public PillarOverviewCacheWorker(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<PillarOverviewCacheWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var refreshInterval = TimeSpan.FromDays(
                _configuration.GetValue("PillarOverviewCache:RefreshIntervalDays", 7));
            var retryDelay = TimeSpan.FromSeconds(
                _configuration.GetValue("PillarOverviewCache:RetryDelaySeconds", 60));
            var failureRetry = TimeSpan.FromMinutes(
                _configuration.GetValue("PillarOverviewCache:FailureRetryMinutes", 60));

            HydrateFromDisk();

            while (!stoppingToken.IsCancellationRequested)
            {
                var savedAt = GetSavedAtUtc();
                var age = savedAt.HasValue
                    ? DateTime.UtcNow - savedAt.Value
                    : TimeSpan.MaxValue;

                if (age < refreshInterval)
                {
                    try
                    {
                        await Task.Delay(refreshInterval - age, stoppingToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }

                    continue;
                }

                var refreshed = await TryRefreshAsync(stoppingToken);
                if (refreshed)
                {
                    continue;
                }

                try
                {
                    await Task.Delay(savedAt.HasValue ? failureRetry : retryDelay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private bool HydrateFromDisk()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var publicService = scope.ServiceProvider.GetRequiredService<IPublicService>();
                if (publicService.HydratePillarOverviewCacheFromDisk())
                {
                    _logger.LogInformation("Pillar overview cache hydrated from disk");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pillar overview disk hydrate failed");
            }

            return false;
        }

        private DateTime? GetSavedAtUtc()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var publicService = scope.ServiceProvider.GetRequiredService<IPublicService>();
                return publicService.GetPillarOverviewCacheSavedAtUtc();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pillar overview cache age lookup failed");
                return null;
            }
        }

        private async Task<bool> TryRefreshAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var publicService = scope.ServiceProvider.GetRequiredService<IPublicService>();
                var preserved = await publicService.RefreshPillarOverviewCacheAsync(stoppingToken);
                var savedAt = publicService.GetPillarOverviewCacheSavedAtUtc();
                var wroteFreshSnapshot = savedAt.HasValue
                    && DateTime.UtcNow - savedAt.Value < TimeSpan.FromMinutes(5);

                if (wroteFreshSnapshot)
                {
                    _logger.LogInformation("Pillar overview cache refreshed");
                    return true;
                }

                if (!preserved)
                {
                    _logger.LogWarning(
                        "Pillar overview cache refresh produced no usable data and no prior snapshot was available");
                }

                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Pillar overview cache refresh failed");
                return false;
            }
        }
    }
}
