
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
            throw new NotImplementedException();
        }

        public List<ToDoThing> GetTodoList()
        {
            var list = Db.Queryable<ToDoThing>().ToList();
            return list;
        }
    }
}
