#if WINDOWS
#else
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfDemo
{
    public class Dummy
    {
        [STAThread]
        public static void Main(string[] args)
        {
        }
    }
}
#endif
