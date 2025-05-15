using SqlSugar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CountTimer.Share
{
    public class DbContext<T> where T : class, new()
    {
        private string FilePath = Environment.CurrentDirectory + @"\Db\ToDoDb.db";

        //注意：不能写成静态的
        public SqlSugarClient Db;//用来处理事务多表查询和复杂的操作
        public SimpleClient<T> CurrentDb { get { return new SimpleClient<T>(Db); } }//用来操作当前表的数据


        public DbContext()
        {


            Db = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = $"Data Source={FilePath}", // SQLite 连接字符串
                DbType = DbType.Sqlite, // 指定数据库类型为 SQLite
                IsAutoCloseConnection = true, // 自动关闭连接
                InitKeyType = InitKeyType.Attribute//从实体特性中读取主键自增列信息
            },
            Db =>
            {
                Db.Aop.OnLogExecuting = (sql, pars) =>
                {
                    Debug.WriteLine($"执行SQL：{sql}");  //输出原始SQL语句到控制台
                };
            });
        }
    }
}
