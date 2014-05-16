using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestSitemaps.Config
{
    public static class AppConfig
    {
        public static AppSettings AppSettings { get; private set; }
        public static ConnectionStrings ConnectionStrings { get; private set; }

        public static void Initialize()
        {
            AppSettings = new AppSettings();
            ConnectionStrings = new ConnectionStrings();
        }
    }
}
