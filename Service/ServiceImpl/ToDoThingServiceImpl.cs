
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Share;
using Dm.util;
using Microsoft.VisualBasic.ApplicationServices;
using SqlSugar;

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
           return Db.Deleteable<ToDoThing>().Where(it => Convert.ToDateTime(it.endTime) < time).ExecuteCommand();
        }
        public int insertRegularEvent()
        {
            List<ToDoThing> things = [];
            things.Add(new ToDoThing() {
                toDoInfo = "下班",
                endTime = DateTime.Today.AddHours(17).AddMinutes(30).ToString("yyyy-MM-dd HH:mm:ss")
            });
            things.Add(new ToDoThing()
            {
                toDoInfo = "午休",
                endTime = DateTime.Today.AddHours(11).AddMinutes(22).ToString("yyyy-MM-dd HH:mm:ss")
            });
            return Db.Insertable(things).ExecuteCommand();
        }
    }
}
