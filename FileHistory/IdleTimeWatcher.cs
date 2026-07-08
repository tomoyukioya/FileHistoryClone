using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileHistory
{
    /// <summary>
    /// アイドル時間を監視し、アイドル時間帯のみクローラを動作させる、バックグランドタスク
    /// </summary>
    public class IdleTimeWatcher: IDisposable
    {
        CancellationTokenSource _cts { get; set; }
        Task _idleWatcherTask { get; set; }
        Crawler _crawler { get; set; }
        Settings _settings { get; set; }
        ILogger _logger { get; set; }

        public IdleTimeWatcher(Settings settings, Crawler crawler, ILoggerFactory loggerFactory)
        {
            _settings = settings;
            _logger = loggerFactory.CreateLogger<IdleTimeWatcher>();
            _crawler = crawler;
            _cts = new CancellationTokenSource();

            // アイドル時にのみクローリング実行
            _idleWatcherTask = Task.Factory.StartNew(() =>
            {
                bool idle = true;
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var idleTime = TimeSpan.FromMilliseconds(IdleTimeFinder.GetIdleTime());
                        if (!idle && idleTime > TimeSpan.FromSeconds(_settings.CrawlingIdleTimer))
                        {
                            Interlocked.Decrement(ref _crawler.CrawlingSuspended);
                            idle = true;
                            _logger.LogInformation($"Detect idle, resume crawling.");
                        }
                        else if (idle && idleTime < TimeSpan.FromSeconds(_settings.CrawlingIdleTimer))
                        {
                            Interlocked.Increment(ref _crawler.CrawlingSuspended);
                            idle = false;
                            _logger.LogInformation($"Idle end, suspend crawling.");
                        }
                        Task.Delay(1000, _cts.Token).Wait();
                    }
                    catch (Exception ex)
                    {
                        if (!_cts.IsCancellationRequested)
                            _logger.LogError($"Exception caught in IdleWatcherTask: {ex}");
                        break;
                    }
                }
                _logger.LogDebug($"IdleWatcherTask End");
            });
        }

        public void Dispose()
        {
            _cts.Cancel();
            Task.WaitAll(_idleWatcherTask);
        }
    }
}
