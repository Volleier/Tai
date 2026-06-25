using Core.Models;
using Core.Models.Db;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Librarys.SQLite
{
    public class TaiDbContext : DbContext
    {
        /// <summary>
        /// 每日数据
        /// </summary>
        public DbSet<DailyLogModel> DailyLog { get; set; }
        /// <summary>
        /// 时段数据
        /// </summary>
        public DbSet<HoursLogModel> HoursLog { get; set; }
        public DbSet<AppModel> App { get; set; }
        /// <summary>
        /// 分类
        /// </summary>
        public DbSet<CategoryModel> Categorys { get; set; }
        /// <summary>
        /// 网站
        /// </summary>
        public DbSet<WebSiteModel> WebSites { get; set; }
        /// <summary>
        /// 网站分类
        /// </summary>
        public DbSet<WebSiteCategoryModel> WebSiteCategories { get; set; }
        /// <summary>
        /// 网页浏览记录（每小时）
        /// </summary>
        public DbSet<WebBrowseLogModel> WebBrowserLogs { get; set; }
        /// <summary>
        /// 网页链接
        /// </summary>
        public DbSet<WebUrlModel> WebUrls { get; set; }

        private static string _dbFilePath = Path.Combine(FileHelper.GetRootDirectory(), "Data", "data.db");
        /// <summary>
        /// 当 DbContext 被 Dispose 时触发的回调，用于释放写入信号量等外部资源。
        /// </summary>
        private Action _onDisposed;

        public TaiDbContext()
       : base(new SQLiteConnection()
       {
           ConnectionString = $"Data Source={_dbFilePath}",
           BusyTimeout = 60
       }, true)
        {
            DbConfiguration.SetConfiguration(new SQLiteConfiguration());
        }

        /// <summary>
        /// 设置一个回调，在 DbContext 被释放时调用。
        /// 用于自动释放写入信号量（SemaphoreSlim），避免因调用方忘记 CloseWriter 导致死锁。
        /// </summary>
        internal void SetOnDisposed(Action action_)
        {
            _onDisposed = action_;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _onDisposed?.Invoke();
                _onDisposed = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            var model = modelBuilder.Build(Database.Connection);
            new SQLiteBuilder(model).SelfCheck();
        }

        public void SelfCheck()
        {
            Database.ExecuteSqlCommand("select count(*) from sqlite_master where type='table' and name='tai'");
        }
    }
}