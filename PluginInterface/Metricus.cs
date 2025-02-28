using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Metricus.Plugin
{
	public abstract class Plugin
	{
		private PluginManager pm;

		public Plugin (PluginManager pm)
		{
			this.pm = pm;
			pm.RegisterPlugin (this);
		}
	
	}

	public abstract class InputPlugin : Plugin
	{
		public InputPlugin(PluginManager pm) : base(pm)
		{
		}

		public abstract List<metric> Work ();
	}


	public abstract class OutputPlugin : Plugin
	{
		public OutputPlugin(PluginManager pm) : base(pm)
		{
		}

		public abstract void Work (List<metric> m);
	}

	public abstract class FilterPlugin : Plugin
	{
		public FilterPlugin(PluginManager pm) : base(pm)
		{
		}

		public abstract List<metric> Work (List<metric> m);
	}

	public class PluginManager
	{
		List<InputPlugin> inputPlugins = new List<InputPlugin>();
		List<OutputPlugin> outputPlugins = new List<OutputPlugin>(); 
		List<FilterPlugin> filterPlugins= new List<FilterPlugin>();

		public string Hostname { get; internal set; }

		public PluginManager(string hostname) { this.Hostname = hostname; }

		public void RegisterInputPlugin( InputPlugin plugin) { inputPlugins.Add (plugin); }

		public void RegisterOutputPlugin( OutputPlugin plugin ) { outputPlugins.Add (plugin); }

		public void RegisterFilterPlugin( FilterPlugin plugin) { filterPlugins.Add (plugin); }

		public void RegisterPlugin( Plugin plugin)
		{
			Console.WriteLine ("Registering plugin of type: " + plugin.GetType().BaseType);
			switch ((plugin.GetType ().BaseType.ToString())) 
			{
			case "Metricus.Plugin.InputPlugin":
				//Console.WriteLine ("Registering InputPlugin");
				this.RegisterInputPlugin ((InputPlugin)plugin);
				break;
			case "Metricus.Plugin.OutputPlugin":
				//Console.WriteLine ("Registering OutputPlugin");
				this.RegisterOutputPlugin ((OutputPlugin)plugin);
				break;
			case "Metricus.Plugin.FilterPlugin":
				//Console.WriteLine ("Registering FilterPlugin");
				this.RegisterFilterPlugin ((FilterPlugin)plugin);
				break;
			default:
				throw new Exception ("Invalid plugin type.");
			}
		}

		public void ListInputPlugins()
		{
			foreach (Plugin plugin in inputPlugins) 
			{
				//Console.WriteLine (plugin.GetType ());
			}
		}

		public void Tick()
		{
			foreach (InputPlugin iPlugin in inputPlugins) 
			{
                var results = iPlugin.Work ();

                foreach (FilterPlugin fPlugin in filterPlugins)
                    results = fPlugin.Work (results);

                foreach ( OutputPlugin oPlugin in outputPlugins)
                    oPlugin.Work(results);
            }
		}

	}

	public struct metric {
		public float value;
		public DateTime timestamp;
		public String category;
		public String type;
		public String instance;
		public int interval;
		public metric( string theCategory, string theType, string theInstance, float theValue, DateTime theTime, int theInterval=10)
		{
			category = theCategory;
			type = theType;
			instance = theInstance;
			value = theValue;
			timestamp = theTime;
			interval = theInterval;
		}

	}

}