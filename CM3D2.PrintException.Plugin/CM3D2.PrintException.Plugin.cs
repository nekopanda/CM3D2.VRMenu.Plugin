using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityInjector;
using UnityInjector.Attributes;

namespace CM3D2.PrintException.Plugin
{
    [PluginName("PrintException"), PluginVersion("0.0.1.0")]
    public class PrintException : PluginBase
    {
        PrintException()
        {
            // 例外の詳細情報を吐かせる
            Application.logMessageReceived += Application_logMessageReceived;
        }

        private static void Application_logMessageReceived(string condition, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    //case LogType.Warning:
                    Console.WriteLine("[" + type + "] " + condition);
                    Console.WriteLine(stackTrace);
                    break;
            }
        }
    }
}
