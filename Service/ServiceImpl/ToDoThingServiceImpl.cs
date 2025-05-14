
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Share;
using SqlSugar;

namespace CountTimer.Service.ServiceImpl
{
    public class ToDoThingServiceImpl : DbContext<ToDoThing>, ToDoThingService
    {
  
    }
}
