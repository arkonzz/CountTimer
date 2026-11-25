
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
        /// <summary>把已经过去的循环事件滚到下一次发生（每天 / 工作日 / 每周各自的规则）</summary>
        public int updateRegularEvent()
        {
            int updated = 0;
            DateTime now = DateTime.Now;
            foreach (ToDoThing toDoThing in GetTodoList())
            {
                if (toDoThing.Kind == RepeatKind.None
                    || !DateTime.TryParse(toDoThing.endTime, out DateTime endTime)
                    || endTime > now)
                {
                    continue;
                }

                string next = RepeatRule.Next(toDoThing.Kind, toDoThing.repeatDay, endTime.TimeOfDay, now).ToString(DateTimeFormat);
                if (next == toDoThing.endTime)
                {
                    continue;
                }

                toDoThing.endTime = next;
                toDoThing.isRegular = true;
                updated += Db.Updateable(toDoThing).ExecuteCommand();
            }

            return updated;
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
