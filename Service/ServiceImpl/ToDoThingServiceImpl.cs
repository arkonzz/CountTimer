
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Share;
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
    }
}
