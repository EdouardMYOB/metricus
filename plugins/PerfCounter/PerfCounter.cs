using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using ServiceStack.Text;
using Metricus.Plugin;

namespace Metricus.Plugins
{
	public class PerfCounter : InputPlugin, IInputPlugin
	{
		private List<PerformanceCounter> performanceCounters = new List<PerformanceCounter>();
		private List<Category> categories = new List<Category>();
		private PerfCounterConfig config;

		public PerfCounter(PluginManager pm) : base(pm)
		{
			var path = Path.GetDirectoryName (Assembly.GetExecutingAssembly().Location);
			config = JsonSerializer.DeserializeFromString<PerfCounterConfig> (File.ReadAllText (path + "/config.json"));
			Console.WriteLine ("Loaded config : {0}", config.Dump ());
			this.LoadCounters ();
		}

		private class PerfCounterConfig {
			public List<ConfigCategory> categories { get; set; }
		}

		private class ConfigCategory {
			public String name { get; set; }
			public bool dynamic { get; set; }
			public List<String> counters { get; set; }
			public List<String> instances { get; set; }
		}

		private class Category {
			public String name { get; set; }
			public Dictionary<Tuple<String, String>, PerformanceCounter> counters { get; set; }
			public bool dynamic { get; set; }

			public Category(string name) {
				this.name = name;
				counters = new Dictionary<Tuple<string, string>, PerformanceCounter>();
			}

		    public void RegisterCounter(String counterName, String instanceName = "") {
				var key = Tuple.Create (counterName, instanceName);
				if( ! counters.ContainsKey(key) ) {
					var counter = new PerformanceCounter (this.name, counterName, instanceName);
					counter.NextValue ();
					this.counters.Add (key, counter);
				}
			}

			public void UnRegisterCounter(String counterName, String instanceName = "") {
				var key = Tuple.Create (counterName, instanceName);
				if (counters.ContainsKey (key)) {
					counters.Remove (key);
				}
			}
		}

		public override List<metric> Work()
		{
			var metrics = new List<metric>();

			foreach( var category in this.categories )
			{
				foreach (var counter in category.counters) {
					var pc = counter.Value;
					try {
						metrics.Add (new metric (pc.CategoryName, pc.CounterName, pc.InstanceName, pc.NextValue (), DateTime.Now));
					} catch (Exception e) {
						Console.WriteLine (e.Message);
					}
				}
			}
			return metrics;
		}

		private void RegisterPerformanceCounters(PerformanceCounter performanceCoutner)
		{
			performanceCoutner.NextValue ();
			this.performanceCounters.Add (performanceCoutner);
		}

		private void LoadCounters()
		{
			var pcList = new List<PerformanceCounter>();
//			pcList.Add (new PerformanceCounter ("Processor", "% Processor Time", "_Total"));		          
//			pcList.Add (new PerformanceCounter ("Memory", "Available MBytes"));
//			foreach (var pc in pcList) { this.RegisterPerformanceCounters (pc); }

			foreach (var configCategory in config.categories) {
				var newCategory = new Category (configCategory.name);
				foreach (var counter in configCategory.counters) {
					if (configCategory.instances != null) {
						foreach (var configInstance in configCategory.instances) {
							Console.WriteLine ("Registering instance {0}", configInstance);
							newCategory.RegisterCounter (counter, configInstance);
						}
					} else {
						var category = new PerformanceCounterCategory (configCategory.name);
						var instanceNames = category.GetInstanceNames ();
						if (instanceNames.Length == 0) {
							newCategory.RegisterCounter (counter);
						} else {
							foreach (var instance in instanceNames) {
								newCategory.RegisterCounter (counter, instance);
							}
						}
					}
				}
				this.categories.Add (newCategory);	
			}


//			foreach (var cat in config.categories) {
//				foreach (var counter in cat.counters) {
//					if (  cat.instances != null ) {
//						foreach (var instance in cat.instances) {
//							this.RegisterPerformanceCounters (new PerformanceCounter (cat.name, counter, instance));
//						}
//					} else {
//						var category = new PerformanceCounterCategory (cat.name);
//						var instanceNames = category.GetInstanceNames ();
//						if (instanceNames.Length == 0) {
//							this.RegisterPerformanceCounters (new PerformanceCounter (cat.name, counter));
//						} else {
//							foreach (var instance in instanceNames) {
//								this.RegisterPerformanceCounters (new PerformanceCounter (cat.name, counter, instance));
//							}
//						}						
//					}
//				}
//			}

		}
	}
}

