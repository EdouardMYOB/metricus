using System;
using System.Collections.Generic;
using Metricus.Plugin;
using Newtonsoft.Json;

namespace Metricus.Plugins
{
	public class ConsoleOut : OutputPlugin, IOutputPlugin
	{
		public ConsoleOut(PluginManager pm) : base(pm) {}

		public override void Work(List<metric> m)
		{
            foreach(var theMetric in m)
            {
                var metricString = JsonConvert.SerializeObject (theMetric, Formatting.Indented);
                Console.WriteLine (metricString);
            }
        }
	}
}