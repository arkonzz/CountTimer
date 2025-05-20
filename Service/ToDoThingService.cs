using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CountTimer.Model;

namespace CountTimer.Service
{
    public interface ToDoThingService
    {
       int AddToDoThing(ToDoThing toDoThing);

       List<ToDoThing> GetTodoList();

       ToDoThing GetById(int id);

        int deleteByTime(DateTime time);
        int updateRegularEvent();


    }
}
