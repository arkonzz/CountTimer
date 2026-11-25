using System.Collections.Generic;
using CountTimer.Model;
using CountTimer.Share;

namespace CountTimer.Service
{
    public interface CustomDayService
    {
        List<CustomDay> GetAll();

        int Add(CustomDay day);

        int Update(CustomDay day);

        int Delete(int id);
    }

    public class CustomDayServiceImpl : DbContext<CustomDay>, CustomDayService
    {
        public List<CustomDay> GetAll()
        {
            return Db.Queryable<CustomDay>()
                .OrderBy(it => it.isLunar)
                .OrderBy(it => it.month)
                .OrderBy(it => it.day)
                .ToList();
        }

        public int Add(CustomDay day)
        {
            return Db.Insertable(day).ExecuteCommand();
        }

        public int Update(CustomDay day)
        {
            return Db.Updateable(day).ExecuteCommand();
        }

        public int Delete(int id)
        {
            return Db.Deleteable<CustomDay>().In(id).ExecuteCommand();
        }
    }
}
