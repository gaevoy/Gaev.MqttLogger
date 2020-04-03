using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Formatter;
using Serilog;
using Serilog.Events;

namespace Gaev.MqttLogger
{
    class Program
    {
        static readonly HashSet<string> TopicsToIgnore = new HashSet<string>
        {
            "zigbee2mqtt/bridge/config",
            "zigbee2mqtt/bridge/state",
            "zigbee2mqtt/bridge/config/devices",
            "zigbee2mqtt/bridge/log",
        };

        static async Task Main(string[] args)
        {
            var logger = Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Debug)
                .WriteTo.Console()
                .WriteTo.Logger(l => l
                    .Filter.ByExcluding(e => e.Level == LogEventLevel.Information)
                    .WriteTo.File("app.log"))
                .WriteTo.Logger(l => l
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Information)
                    .WriteTo.File("event.log", outputTemplate: "{Timestamp:o} {Message}{NewLine}"))
                .CreateLogger();
            var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (__, e) =>
            {
                logger.Debug("Stopping");
                e.Cancel = true;
                cancellation.Cancel();
            };
            var options = new MqttClientOptions
            {
                ClientId = "GaevMqttLogger",
                ProtocolVersion = MqttProtocolVersion.V500,
                ChannelOptions = new MqttClientTcpOptions {Server = "127.0.0.1", Port = 1883},
                CleanSession = false
            };
            logger.Debug("Started");
            using var cli = new MqttFactory().CreateMqttClient();
            await ConnectToMqtt(cli, options, logger, cancellation);
            await WaitForCancellation(cancellation.Token);
            logger.Debug("Stopped");
            Log.CloseAndFlush();
        }

        private static async Task WaitForCancellation(CancellationToken cancellation)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellation);
            }
            catch (TaskCanceledException)
            {
            }
        }

        private static async Task ConnectToMqtt(
            IMqttClient cli,
            MqttClientOptions options,
            ILogger logger,
            CancellationTokenSource cancellation
        )
        {
            cli.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(arg =>
            {
                var msg = arg.ApplicationMessage;
                var payload = Encoding.UTF8.GetString(msg.Payload);
                if (!TopicsToIgnore.Contains(msg.Topic))
                    logger.Information(msg.Topic + " " + payload);
            });
            cli.ConnectedHandler = new MqttClientConnectedHandlerDelegate(async _ =>
            {
                await cli.SubscribeAsync(new TopicFilterBuilder()
                    .WithTopic("zigbee2mqtt/#")
                    .WithAtLeastOnceQoS()
                    .Build());
            });
            cli.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(async _ =>
            {
                logger.Debug("Reconnecting");
                await Task.Delay(TimeSpan.FromSeconds(2));

                try
                {
                    await cli.ConnectAsync(options, cancellation.Token);
                    logger.Debug("Reconnected");
                }
                catch (Exception ex)
                {
                    logger.Debug("Reconnecting failure: " + ex.Message);
                }
            });

            logger.Debug("Connecting");
            try
            {
                await cli.ConnectAsync(options, cancellation.Token);
                logger.Debug("Connected");
            }
            catch (Exception ex)
            {
                logger.Debug("Connecting failure: " + ex.Message);
            }
        }
    }
}