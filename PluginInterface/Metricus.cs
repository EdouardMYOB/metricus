using System;
using System.Collections.Generic;

namespace Metricus.Plugin
{
    public abstract class Plugin
    {
        private PluginManager _pm;

        protected Plugin (PluginManager pm)
        {
            this._pm = pm;
            pm.RegisterPlugin (this);
        }
    
    }

    public abstract class InputPlugin : Plugin
    {
        protected InputPlugin(PluginManager pm) : base(pm)
        {
        }

        public abstract List<metric> Work ();
    }


    public abstract class OutputPlugin : Plugin
    {
        protected OutputPlugin(PluginManager pm) : base(pm)
        {
        }

        public abstract void Work (List<metric> m);
    }

    public abstract class FilterPlugin : Plugin
    {
        protected FilterPlugin(PluginManager pm) : base(pm)
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
            var baseType = plugin.GetType().BaseType;
            Console.WriteLine ("Registering plugin of type: " + baseType);
            switch ((baseType?.ToString())) 
            {
            case "Metricus.Plugin.InputPlugin":
                this.RegisterInputPlugin ((InputPlugin)plugin);
                break;
            case "Metricus.Plugin.OutputPlugin":
                this.RegisterOutputPlugin ((OutputPlugin)plugin);
                break;
            case "Metricus.Plugin.FilterPlugin":
                this.RegisterFilterPlugin ((FilterPlugin)plugin);
                break;
            default:
                throw new Exception ("Invalid plugin type.");
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

    public struct metric
    {
        public float value;
        public DateTime timestamp;
        public string category;
        public string type;
        public string instance;
        public int interval;
        public string site;

        public metric(string theCategory, string theType, string theInstance, float theValue, DateTime theTime,
            int theInterval = 10)
        {
            site = null;
            category = theCategory;
            type = theType;
            instance = theInstance;
            value = theValue;
            timestamp = theTime;
            interval = theInterval;
        }

        public override string ToString()
        {
            return
                $"{{'site':'{site}','category':'{category}','type':'{type}','instance':'{instance}','value':{value},'timestamp':'{timestamp:yyyy-MM-dd HH:mm:ss}','interval':{interval}}}";
        }
    }


}

