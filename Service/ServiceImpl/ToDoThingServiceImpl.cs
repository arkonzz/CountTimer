
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Share;
using SqlSugar;

namespace CountTimer.Service.ServiceImpl
{
    public class ToDoThingServiceImpl : DbContext<ToDoThing>, ToDoThingService
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public int AddToDoThing(ToDoThing toDoThing)
        {
           return Db.Insertable(toDoThing).ExecuteCommand();
        }

        public List<ToDoThing> GetTodoList()
        {
            var list = Db.Queryable<ToDoThing>().OrderBy(it => it.endTime).ToList();
            return list;
        }
        public ToDoThing GetById(int id) {
            return Db.Queryable<ToDoThing>().InSingle(id);
        }

        public int deleteByTime(DateTime time)
        {
            string endTime = time.ToString(DateTimeFormat);
            const string sql = @"DELETE FROM to_do_list
WHERE is_regular = 0 AND datetime(end_time) < datetime(@endTime)";

            return Db.Ado.ExecuteCommand(sql, new SugarParameter("@endTime", endTime));
        }
        public int updateRegularEvent()
        {
            const string sql = @"UPDATE to_do_list
SET end_time = CASE
    WHEN time(end_time) <= time('now', 'localtime')
        THEN datetime(date('now', 'localtime', '+1 day') || ' ' || time(end_time))
    ELSE datetime(date('now', 'localtime') || ' ' || time(end_time))
END
WHERE is_regular = 1 AND datetime(end_time) < datetime('now', 'localtime')";
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
