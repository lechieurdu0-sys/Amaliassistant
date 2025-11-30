using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameOverlay.Windows
{
    public class WebNavigationHistory
    {
        public List<string> History { get; set; } = new List<string>();
        public int CurrentIndex { get; set; } = -1;
    }
}



