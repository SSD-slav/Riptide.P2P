using RiptideNetworking;
using RiptideNetworking.Experimental.P2P;
using RiptideNetworking.Utils;
using System;
using System.Threading;

namespace RiptideDemos.Experimental.P2P
{
    internal class Program
    {
        private static P2PNetwork network = new P2PNetwork("127.0.0.1");
        public static void Main(string[] args)
        {
            RiptideLogger.Initialize(
                log => Console.WriteLine("DEBUG: " + log),
                log => Console.WriteLine("INFO: " + log),
                log => Console.WriteLine("WARNING: " + log),
                log => Console.WriteLine("ERROR: " + log),
                true);
            
            network.MessageReceived += MessageReceived;
            network.OnReady += OnReady;
            Console.WriteLine("Started");
        }

        private static void OnReady(object sender, OnReadyArgs e)
        {
            Console.WriteLine("Ready");
            Message message = Message.Create(MessageSendMode.reliable, 0);
            message.Add("I am alive");
            network.BroudcastToNetwork(message);
            while (true)
            {
                network.Tick();
                Thread.Sleep(TimeSpan.FromSeconds(1f/3f));
            }
        }

        private static void MessageReceived(object sender, MessageArgs e)
        {
            Console.WriteLine(e.message.GetString());
        }
    }
}
