using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BrickSetManager.Database;

namespace BrickSetManager.Services
{
    public class SetQueueProcessor
    {
        private readonly ConcurrentQueue<SetQueueItem> _setQueue;
        private readonly Scraper _scraper;
        private readonly SetRepository _setRepository;
        private readonly BrickRepository _brickRepository;
        private readonly InventoryRepository _inventoryRepository;
        private bool _isProcessing;
        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<SetProcessingEventArgs> SetProcessingStarted;
        public event EventHandler<SetProcessingEventArgs> SetProcessingCompleted;
        public event EventHandler<SetProcessingEventArgs> SetProcessingFailed;
        public event EventHandler QueueEmptied;

        public int QueueCount => _setQueue.Count;
        public bool IsProcessing => _isProcessing;

        public SetQueueProcessor()
        {
            _setQueue = new ConcurrentQueue<SetQueueItem>();
            _scraper = new Scraper();
            _setRepository = new SetRepository();
            _brickRepository = new BrickRepository();
            _inventoryRepository = new InventoryRepository();
        }

        public void EnqueueSet(string setNumber, int quantity = 1, int inventoryVersion = 2)
        {
            _setQueue.Enqueue(new SetQueueItem
            {
                SetNumber = setNumber,
                Quantity = quantity,
                InventoryVersion = inventoryVersion
            });

            // Start processing if not already running
            if (!_isProcessing)
            {
                StartProcessing();
            }
        }

        private async void StartProcessing()
        {
            if (_isProcessing)
                return;

            _isProcessing = true;
            _cancellationTokenSource = new CancellationTokenSource();

            await Task.Run(async () =>
            {
                while (_setQueue.TryDequeue(out SetQueueItem item))
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    await ProcessSetAsync(item.SetNumber, item.Quantity, item.InventoryVersion);
                }

                _isProcessing = false;
                QueueEmptied?.Invoke(this, EventArgs.Empty);
            });
        }

        private async Task ProcessSetAsync(string setNumber, int quantity, int inventoryVersion)
        {
            try
            {
                // Notify that processing has started
                SetProcessingStarted?.Invoke(this, new SetProcessingEventArgs
                {
                    SetNumber = setNumber,
                    Status = "Scraping set data..."
                });

                // Scrape the set data
                var (set, inventory) = await _scraper.ScrapeSetAsync(setNumber, inventoryVersion);

                // Set the quantity and inventory version from the queue
                set.Quantity = quantity;
                set.InventoryVersion = inventoryVersion;

                // Save to database
                SetProcessingStarted?.Invoke(this, new SetProcessingEventArgs
                {
                    SetNumber = setNumber,
                    Status = "Saving to database..."
                });

                _setRepository.AddSet(set);

                foreach (var item in inventory)
                {
                    _brickRepository.AddBrick(item.BrickDetail);
                    _inventoryRepository.AddInventoryItem(item);
                }

                // Notify completion
                SetProcessingCompleted?.Invoke(this, new SetProcessingEventArgs
                {
                    SetNumber = setNumber,
                    SetName = set.SetName,
                    PartCount = inventory.Count,
                    Status = $"Successfully added {inventory.Count} parts"
                });
            }
            catch (Exception ex)
            {
                // Notify failure
                SetProcessingFailed?.Invoke(this, new SetProcessingEventArgs
                {
                    SetNumber = setNumber,
                    Status = $"Error: {ex.Message}"
                });
            }
        }

        public void CancelProcessing()
        {
            _cancellationTokenSource?.Cancel();
        }
    }

    public class SetProcessingEventArgs : EventArgs
    {
        public string SetNumber { get; set; }
        public string SetName { get; set; }
        public int PartCount { get; set; }
        public string Status { get; set; }
    }

    public class SetQueueItem
    {
        public string SetNumber { get; set; }
        public int Quantity { get; set; }
        public int InventoryVersion { get; set; }
    }
}
