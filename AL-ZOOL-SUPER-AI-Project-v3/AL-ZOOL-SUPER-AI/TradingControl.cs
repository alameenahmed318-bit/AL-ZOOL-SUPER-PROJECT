using System;
using System.Threading;
using System.Threading.Tasks;

namespace ALZOOOOL
{
    public sealed class TradingControl
    {
        private int _isRunning;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public bool IsRunning =>
            Volatile.Read(ref _isRunning) == 1;

        public event Action<bool>? TradingStateChanged;

        public void Start()
        {
            Interlocked.Exchange(ref _isRunning, 1);
            TradingStateChanged?.Invoke(true);
        }

        public async Task StopAsync(
            Func<Task> cancelPendingOrders)
        {
            await _lock.WaitAsync();

            try
            {
                // إيقاف فتح الصفقات فوراً
                Interlocked.Exchange(ref _isRunning, 0);
                TradingStateChanged?.Invoke(false);

                // إلغاء أوامر الدخول المعلقة
                await cancelPendingOrders();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> TryOpenTradeAsync(
            Func<Task<bool>> sendOrder)
        {
            await _lock.WaitAsync();

            try
            {
                // فحص الإيقاف قبل إرسال الأمر
                if (!IsRunning)
                    return false;

                return await sendOrder();
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}