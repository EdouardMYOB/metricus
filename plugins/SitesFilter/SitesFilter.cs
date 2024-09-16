using Microsoft.Web.Administration;
using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Metricus.Plugin
{
    interface ICategoryFilter
    {
        List<metric> Filter(List<metric> metrics, string categoryName, bool preserveOriginal);
    }

    public class SitesFilter : FilterPlugin, IFilterPlugin
	{
		private SitesFilterConfig config;
		private Dictionary<int, string> siteIDtoName;
        private ServerManager ServerManager;
        private System.Timers.Timer LoadSitesTimer;
        private Object RefreshLock = new Object();

		private class SitesFilterConfig {
			public Dictionary<string,ConfigCategory> Categories { get; set; }
            public bool Debug { get; set; }

        }

        private class ConfigCategory {
            public List<string> Filters { get; set; }
			public bool PreserveOriginal { get; set; }
		}

		public SitesFilter(PluginManager pm) : base(pm)	{
			var path = Path.GetDirectoryName (Assembly.GetExecutingAssembly().Location);
			var configFile = path + "/config.json";
			Console.WriteLine ("Loading config from {0}", configFile);
			config = JsonSerializer.DeserializeFromString<SitesFilterConfig> (File.ReadAllText (path + "/config.json"));
			Console.WriteLine ("Loaded config : {0}", config.Dump ());
			siteIDtoName = new Dictionary<int, string> ();
			this.LoadSites ();
            LoadSitesTimer = new System.Timers.Timer(300000);
            LoadSitesTimer.Elapsed += (o, e) => this.LoadSites();
            LoadSitesTimer.Start();
		}

		public override List<metric> Work(List<metric> m)
		{
		    lock (RefreshLock)
		    {
		        var filterMap = new Dictionary<string, ICategoryFilter>
		        {
		            {"w3wp.process", new FilterWorkerPoolProcesses(ServerManager, "Process", "ID Process", config.Debug)},
		            {"w3wp.net", new FilterWorkerPoolProcesses(ServerManager, ".NET CLR Memory", "Process ID", config.Debug)},
		            {"lmw3svc", new FilterAspNetC(this.siteIDtoName, config.Debug)},
		            {"w3svc", new FilterW3SvcW3Wp(config.Debug)}
		        };

		        foreach (var category in config.Categories)
		            foreach (var filter in category.Value.Filters)
		                if (filterMap.ContainsKey(filter))
		                    m = filterMap[filter].Filter(m, category.Key, category.Value.PreserveOriginal);
		    }

			return m;
		}

        public class FilterWorkerPoolProcesses : ICategoryFilter
        {
            public static Dictionary<string, int> WpNamesToIds = new Dictionary<string, int>();

            private ServerManager serverManager;
            private readonly string processIdCounter;
            private readonly bool isDebug;
            private readonly string processIdCategory;

            public FilterWorkerPoolProcesses(ServerManager serverManager, string processIdCategory, string processIdCounter, bool isDebug = false)
            {
                this.serverManager = serverManager;
                this.processIdCounter = processIdCounter;
                this.isDebug = isDebug;
                this.processIdCategory = processIdCategory;
            }

            public List<metric> Filter(List<metric> metrics, string categoryName, bool preserveOriginal)
            {
                // "Listen" to the process id counters to map instance names to process id's
                metric currentMetric;
                var originalMetricsCount = metrics.Count;
                for (int x = 0; x < originalMetricsCount; x++)
                {
                    currentMetric = metrics[x];

                    if (!processIdCategory.Equals(currentMetric.category, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    if (currentMetric.type.Equals(processIdCounter, StringComparison.InvariantCultureIgnoreCase))
                    {
                        WpNamesToIds[currentMetric.instance] = (int) currentMetric.value;
                        continue;
                    }
                }
                for (int x = 0; x < originalMetricsCount; x++)
                { 
                    currentMetric = metrics[x];

                    if(!currentMetric.category.Equals(categoryName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    if (currentMetric.category.Equals(processIdCategory, StringComparison.InvariantCultureIgnoreCase) &&
                        currentMetric.type.Equals(processIdCounter, StringComparison.InvariantCultureIgnoreCase))
                    {

                        continue; // Don't transform the "Process ID" values as all the other transformations rely on it
                    }
                    if (currentMetric.instance.StartsWith("w3wp", StringComparison.Ordinal) && WpNamesToIds.TryGetValue(currentMetric.instance, out var wpId))
                    {
                        for (int y = 0; y < serverManager.WorkerProcesses.Count; y++)
                        {
                            if (serverManager.WorkerProcesses[y].ProcessId == wpId)                            
                            {
                                var siteName = serverManager.WorkerProcesses[y].AppPoolName;

                                var newMetric = currentMetric;

                                newMetric.site = siteName;
                                newMetric.instance = null;
                                if(isDebug)
                                {
                                    Console.WriteLine($"old: {currentMetric}");
                                    Console.WriteLine($"new: {newMetric}");
                                }

                                if (preserveOriginal)
                                    metrics.Add(currentMetric);
                                else
                                    metrics[x] = newMetric;
                            }
                        }
                    }                    
                }
                return metrics;
            }
        }

	    public class FilterW3SvcW3Wp : ICategoryFilter
        {
            private readonly bool _isDebug;
            private static Regex AppPoolRegex = new Regex(@"\d+_(?<AppPool>.*)");

            public FilterW3SvcW3Wp(bool isDebug = false)
            {
                _isDebug = isDebug;
            }

            public List<metric> Filter(List<metric> metrics, string categoryName, bool preserveOriginal)
            {
                var returnMetrics = new List<metric>();

                foreach (var metric in metrics)
                {
                    
                    if (!metric.category.Equals(categoryName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // not interested in this metric
                        returnMetrics.Add(metric);
                        continue;
                    }

                    var match = AppPoolRegex.Match(metric.instance);
                    if (!match.Success)
                    {
                        // this metric doesn't have an app pool name in the instance name
                        returnMetrics.Add(metric);
                    }
                    else
                    {
                        //grab the app pool name and name the metric instance to this
                        var siteName = match.Groups["AppPool"].Value;

                        var newMetric = metric;
                        newMetric.site = siteName;
                        newMetric.instance = siteName;

                        if(_isDebug)
                        {
                            Console.WriteLine($"old: {metric}");
                            Console.WriteLine($"new: {newMetric}");
                        }

                        if (preserveOriginal)
                            returnMetrics.Add(metric);
                    }
                }

                return returnMetrics;
            }
        }

        public class FilterAspNetC : ICategoryFilter
        {
            private static string PathSansId = "_LM_W3SVC";
            private static Regex MatchPathWithId = new Regex("_LM_W3SVC_(\\d+)_");
            private static Regex MatchRoot = new Regex("ROOT_?");

            private readonly Dictionary<int, string> siteIdsToNames;
            private readonly bool _isDebug;

            public FilterAspNetC(Dictionary<int, string> siteIdsToNames, bool isDebug = false)
            {
                this.siteIdsToNames = siteIdsToNames;
                _isDebug = isDebug;
            }

            public List<metric> Filter(List<metric> metrics, string categoryName, bool preserveOriginal)
            {
                var returnMetrics = new List<metric>();
                foreach (var metric in metrics)
                {
                    // copy 
                    var newMetric = metric;

                    if (!metric.category.Equals(categoryName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        returnMetrics.Add(newMetric);
                        continue;
                    }

                    if (metric.instance.Contains(PathSansId))
                    {
                        var match = MatchPathWithId.Match(metric.instance);
                        var id = match.Groups[1].Value;
                        if (siteIdsToNames.TryGetValue(int.Parse(id), out string siteName))
                        {
                            newMetric.site = siteName;
                            newMetric.instance = siteName;
                            if (_isDebug)
                            {
                                Console.WriteLine($"old: {metric}");
                                Console.WriteLine($"new: {newMetric}");
                            }
                            
                            returnMetrics.Add(newMetric);
                        }
                        if (preserveOriginal)
                            returnMetrics.Add(metric);
                    }
                    else
                        returnMetrics.Add(newMetric);                    
                }
                return returnMetrics;
            }

        }

        public static string SiteNameReplacement(string siteName) => $"##{siteName}##";


        public void LoadSites() {
		    lock (RefreshLock)
		    {
		        try
		        {
		            ServerManager?.Dispose();
		            ServerManager = new Microsoft.Web.Administration.ServerManager();
		            siteIDtoName.Clear();
		            foreach (var site in ServerManager.Sites)
		            {
		                this.siteIDtoName.Add((int) site.Id, site.Name);
		            }

		            this.siteIDtoName.PrintDump();
		        }
		        catch (Exception ex)
                {
                    Console.WriteLine($"Exception {ex} while loading IIS site information");
                }
		    }
        } 
	}
}