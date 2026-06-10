using Core.Librarys.SQLite;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Servicers.Instances
{
    /// <summary>
    /// 数据库访问协调器。
    /// - 读操作：每次 GetReaderContext() 创建独立的 TaiDbContext，无锁，并发安全。
    /// - 写操作：通过 SemaphoreSlim(1,1) 串行化，保证同一时间只有一个写入者。
    /// - 写入 DbContext 在 Dispose 时自动释放信号量，无需手工调用 CloseWriter。
    ///
    /// 相比旧版改进：
    /// - 移除 Thread.Sleep(1000) 忙等轮询 → SemaphoreSlim.Wait
    /// - 移除 _readerNum / _isReading / _isWriting 脆弱状态位
    /// - 移除 GetReaderContext 中释放 Writer 上下文的错误行为
    /// - 统一由 using 块管理 DbContext 生命周期
    /// </summary>
    public class Database : IDatabase
    {
        private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
        /// <summary>
        /// 原子标记：0 = 信号量未持有，1 = 信号量已持有。
        /// 防止 CloseWriter 和 DbContext.Dispose 双重释放。
        /// </summary>
        private int _writeSemaphoreHeld = 0;

        public TaiDbContext GetReaderContext()
        {
            //  读操作：直接创建新上下文，无需锁
            return new TaiDbContext();
        }

        public TaiDbContext GetWriterContext()
        {
            //  写操作：获取信号量，保证串行写入
            _writeSemaphore.Wait();
            Interlocked.Exchange(ref _writeSemaphoreHeld, 1);

            var context = new TaiDbContext();

            //  注册 Dispose 回调：即使调用方忘记 CloseWriter，dispose 时也会自动释放
            var self = this;
            context.SetOnDisposed(() => self.TryReleaseWriter());

            return context;
        }

        /// <summary>
        /// 尝试释放写入信号量。幂等：多次调用只有第一次生效。
        /// </summary>
        private void TryReleaseWriter()
        {
            if (Interlocked.Exchange(ref _writeSemaphoreHeld, 0) == 1)
            {
                _writeSemaphore.Release();
            }
        }
    }
}
