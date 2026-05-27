
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Share;
using Dm;
using Dm.util;
using Microsoft.VisualBasic.ApplicationServices;
using SqlSugar;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CountTimer.Service.ServiceImpl
{
    public class ToDoThingServiceImpl : DbContext<ToDoThing>, ToDoThingService
    {
        public int AddToDoThing(ToDoThing toDoThing)
        {
           return Db.Insertable(toDoThing).ExecuteCommand();
        }

        public List<ToDoThing> GetTodoList()
        {
            var list = Db.Queryable<ToDoThing>().ToList();
            return list;
        }
        public ToDoThing GetById(int id) {
            return Db.Queryable<ToDoThing>().InSingle(id);
        }

        public int deleteByTime(DateTime time)
        {
           return Db.Deleteable<ToDoThing>().Where(it => Convert.ToDateTime(it.endTime) < time&&it.isRegular==false).ExecuteCommand();
        }
        public int updateRegularEvent()
        {
            string sql = @"UPDATE to_do_list 
            SET end_time = date('now','localtime') || ' ' || time(end_time) 
            WHERE is_regular = 1";
            return Db.Ado.ExecuteCommand(sql);
        }

        public int deleteById(int id)
        {
           return Db.Deleteable<ToDoThing>().In(id).ExecuteCommand();
        }

        public int UpdateToDoThing(ToDoThing toDoThing)
        {
            return Db.Updateable(toDoThing).ExecuteCommand();
        }
    }
}
