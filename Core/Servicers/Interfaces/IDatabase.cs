using Core.Librarys.SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Servicers.Interfaces
{
    /// <summary>
    /// 数据库访问协调接口。
    /// 每次调用 GetReaderContext/GetWriterContext 都创建独立的 TaiDbContext，
    /// 由 using 块负责生命周期。写入通过 SemaphoreSlim 串行化，SQLite 的
    /// BusyTimeout 提供额外的超时保护。
    /// </summary>
    public interface IDatabase
    {
        /// <summary>
        /// 创建一个新的只读 DbContext（并发安全，无锁）。
        /// </summary>
        TaiDbContext GetReaderContext();

        /// <summary>
        /// 创建一个新的写入 DbContext（内部通过 SemaphoreSlim 串行化）。
        /// 返回的 DbContext 在 Dispose 时会自动释放写入信号量，
        /// 调用方无需再手工调用 CloseWriter。
        /// </summary>
        TaiDbContext GetWriterContext();
    }
}
