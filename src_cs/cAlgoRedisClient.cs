using cAlgo.API;
using CSRedis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarbonFx.FOS
{
    public class cAlgoRedisClient : Robot
    {
        protected RedisClient _redisClientOrderChannel;
        protected RedisClient _redisClientQuote;
        protected RedisClient _redisClientCmd;

        protected bool _shuttingDown = false;

        protected string order_channel_listen = "incoming";
        protected string order_channel_publish = "outgoing";
        protected string quote_channel = "tw_quote";
        protected int order_expiration = 5;
        protected bool include_depth = false;

        protected Queue<JObject> quote_queue = new Queue<JObject>();

        protected bool Quotes = false;
        protected List<string> symbols = new List<string>();

        protected string redisHost = "localhost";

        protected int redisPort = 6379;

        System.Collections.Hashtable requirePayloads = new System.Collections.Hashtable();
        System.Collections.Hashtable alreadyHandledActions = new System.Collections.Hashtable();

        protected override void OnStart()
        {
            requirePayloads.Add("create_order", null);
            requirePayloads.Add("cancel_order", null);


            _redisClientOrderChannel = new RedisClient(redisHost, redisPort);
            _redisClientQuote = new RedisClient(redisHost, redisPort);


            _redisClientOrderChannel.SubscriptionReceived += (s, e) =>
            {
                this.Print("DEBUG: {0}", e.Message.Body);
                try
                {
                    var message = ParseMessage(e.Message.Body);
                    if (message == null) return;

                    switch ((string)message["cmd"])
                    {
                        case "create_order":
                            TradeResult result = CreateOrder((JObject)message["payload"]);
                            if (result != null)
                            {
                                HandleTradeResult(result, (JObject)message["payload"]);
                            }
                            break;

                        case "get_orders":
                            HandleGetOrders((JObject)message["payload"]);
                            break;

                        case "cancel_order":
                            HandleCloseOrder((JObject)message["payload"]);
                            break;

                        case "cancel_orders_all":
                            HandleCloseAllOrders((JObject)message["payload"]);
                            break;

                        case "get_positions":
                            HandleGetPositions((JObject)message["payload"]);
                            break;

                        case "close_position":
                            HandleClosePosition((JObject)message["payload"]);
                            break;

                        case "modify_position":
                            HandleModifyPosition((JObject)message["payload"]);
                            break;

                        case "get_time":
                            HandleGetTime((JObject)message["payload"]);
                            break;
                        default:
                            //this.Print("Unhandled message", (string)message["cmd"]);
                            break;

                    }
                }
                catch (Exception ex)
                {
                    this.Print("Error processing request:" + ex.Message + "---" + ex.Source);
                    this.Print(ex.StackTrace.Replace('\n', '-'));
                }
            };

            _redisClientOrderChannel.SubscriptionChanged += (s, e) =>
            {
                Console.WriteLine("There are now {0} open channels", e.Response.Count);
            };

            _redisClientOrderChannel.Connected += (s, e) =>
            {
                this.Print("_redisClient connected");
            };

            string ping = _redisClientOrderChannel.Ping();
            this.Print(ping);
            this.Print(_redisClientOrderChannel.Time());
            new Task(() =>
            {
                this.Positions.Opened += (e) =>
                {
                    try
                    {
                        this.Print("Position opened {0}", e.Position.Id);
                        // already handled
                        var key = _getPositionString(e.Position, "market_order");
                        if (alreadyHandledActions.ContainsKey(key))
                        {
                            alreadyHandledActions.Remove(key);
                            return;
                        }
                        HandlePositionOpened(e.Position);
                    }
                    catch (Exception ex)
                    {
                        this.Print("Error Positions.Opened:" + ex.Message + "---" + ex.Source);
                        this.Print(ex.StackTrace.Replace('\n', '-'));
                    }
                };
                this.Positions.Closed += (e) =>
                {
                    try
                    {
                        this.Print("Position closed {0}", e.Position.Id);
                        var key = _getPositionString(e.Position, "close_position");
                        if (alreadyHandledActions.ContainsKey(key))
                        {
                            alreadyHandledActions.Remove(key);
                            return;
                        }
                        HandlePositionClosed(e.Position);
                    }
                    catch (Exception ex)
                    {
                        this.Print("Error Positions.Closed:" + ex.Message + "---" + ex.Source);
                        this.Print(ex.StackTrace.Replace('\n', '-'));
                    }
                };
            }).Start();

            foreach (var sym in symbols)
            {
                var symbol = this.MarketData.GetSymbol(sym);
                this.MarketData.GetMarketDepth(symbol).Updated += () =>
                {
                    var book = this.MarketData.GetMarketDepth(symbol);
                    var askDepth = book.AskEntries.Sum(e => e.Volume);
                    var bidDepth = book.BidEntries.Sum(e => e.Volume);


                    var asks = this.include_depth ? book.AskEntries.Select(t => new JObject(new JProperty(t.Price.ToString(), t.Volume))) : null;
                    var bids = this.include_depth ? book.BidEntries.Select(t => new JObject(new JProperty(t.Price.ToString(), t.Volume))) : null;

                    JObject o = new JObject(
                       new JProperty("cmd", "tick"),
                       new JProperty("payload", new JObject(
                            new JProperty("symbol", symbol.Code.ToLower()),
                            new JProperty("bid", symbol.Bid),
                            new JProperty("ask", symbol.Ask),
                            new JProperty("bidDepth", bidDepth),
                            new JProperty("askDepth", askDepth),
                            new JProperty("bids", bids),
                            new JProperty("asks", asks),
                            new JProperty("pip_size", symbol.PipSize),
                            new JProperty("spread", Math.Round(symbol.Spread / symbol.PipSize, 1)),
                            new JProperty("time", this.Server.Time.ToString("yyyy-MM-dd HH:mm:ss.FFFZ", CultureInfo.InvariantCulture))
                           )
                       ));

                    quote_queue.Enqueue(o);
                };
            }

            new Task(() =>
            {
                while (!_shuttingDown)
                {
                    JObject o = new JObject(
                       new JProperty("cmd", "account_status"),
                       new JProperty("payload", new JObject(
                            new JProperty("acct_id", this.Account.Number),
                            new JProperty("is_live", this.Account.IsLive),
                            new JProperty("balance", this.Account.Balance),
                            new JProperty("equity", this.Account.Equity),
                            new JProperty("unrealized_gross_profit", this.Account.UnrealizedGrossProfit)
                           )
                       ));

                    _publishMessage(o);

                    Thread.Sleep(1000 * 10);
                }
            }).Start();

            new Task(() =>
            {
                while (Quotes && !_shuttingDown)
                {
                    if (!_redisClientQuote.IsConnected) _redisClientQuote.Connect(5000);
                    if (quote_queue.Count > 0)
                    {
                        _redisClientQuote.StartPipe();
                        while (quote_queue.Count > 0)
                        {
                            JObject quote = quote_queue.Dequeue();
                            _redisClientQuote.Publish(quote_channel, quote.ToString(Formatting.None));
                        }
                        _redisClientQuote.EndPipe();
                    }
                    Thread.Sleep(50);
                }
            }).Start();

            new Task(() =>
            {
                try
                {
                    _redisClientOrderChannel.PSubscribe(order_channel_listen);
                }
                catch (Exception e)
                {
                    this.Print(e.Message);
                }
            }).Start();

            base.OnStart();
        }

        private void _shutdown()
        {
            _shuttingDown = true;
            try
            {
                if (_redisClientOrderChannel.IsConnected)
                {
                    _redisClientOrderChannel.Quit();
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                _redisClientOrderChannel.Dispose();
            }

            try
            {
                if (_redisClientCmd.IsConnected)
                {
                    _redisClientCmd.Quit();
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                _redisClientCmd.Dispose();
            }

            try
            {
                if (_redisClientQuote.IsConnected)
                {
                    _redisClientQuote.Quit();
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                _redisClientQuote.Dispose();
            }
        }

        protected override void OnStop()
        {
            _shutdown();
            base.OnStop();
        }

        protected override void OnError(Error error)
        {
            this.Print("Trade, error {0}, ", error.Code);

            JObject o = new JObject(
               new JProperty("cmd", "on_error"));

            if (error.TradeResult.PendingOrder != null)
            {
                o.Add("payload", new JObject(
                    new JProperty("label", error.TradeResult.PendingOrder.Label),
                    new JProperty("_id", error.TradeResult.PendingOrder.Id),
                    new JProperty("id", error.TradeResult.PendingOrder.Label.Split('|')[0]),
                    new JProperty("symbol", error.TradeResult.PendingOrder.SymbolCode.ToLower())));

            }
            else if (error.TradeResult.Position != null)
            {
                o.Add("payload", new JObject(
                   new JProperty("label", error.TradeResult.Position.Label),
                   new JProperty("_id", error.TradeResult.Position.Id),
                   new JProperty("id", error.TradeResult.Position.Label.Split('|')[0]),
                   new JProperty("symbol", error.TradeResult.Position.SymbolCode.ToLower())));
            }

            _publishMessage(o);
        }

        private void HandleGetTime(JObject message)
        {
            JObject o = new JObject(
              new JProperty("cmd", "get_time_result"),
              new JProperty("_callbackId", message.ContainsKey("_callbackId") ? (string)message["_callbackId"] : null),
              new JProperty("payload", new JObject(
                   new JProperty("time", this.Server.Time.ToUnixTime())
                  )));

            _publishMessage(o);
        }

        List<Position> closedPositions = new List<Position>();
        private void HandleClosePosition(JObject message)
        {
            var id = (string)message["id"];
            foreach (var p in this.Positions)
            {
                if (p.Id.ToString() == id)
                {

                    alreadyHandledActions.Add(_getPositionString(p, "close_position"), null);
                    TradeResult result = this.ClosePosition(p);
                    if (result.IsSuccessful)
                    {
                        JObject o = new JObject(
                           new JProperty("cmd", "position_closed_result"),
                           new JProperty("_callbackId", message.ContainsKey("_callbackId") ? (string)message["_callbackId"] : null),
                           new JProperty("payload", new JObject(
                               new JProperty("label", p.Label),
                               new JProperty("_id", p.Id),
                               new JProperty("id", p.Label.Split('|')[0]),
                               new JProperty("symbol", p.SymbolCode.ToLower()),
                               new JProperty("pips", p.Pips),
                               new JProperty("net_profit", p.NetProfit),
                               new JProperty("gross_profit", p.GrossProfit),
                               new JProperty("ctime", DateTime.UtcNow.ToUnixTime())
                               )
                       ));

                        _publishMessage(o);
                    }
                    else
                    {
                        JObject o = new JObject(
                          new JProperty("cmd", "position_closed_failed_result"),
                          new JProperty("_callbackId", message.ContainsKey("_callbackId") ? (string)message["_callbackId"] : null),
                          new JProperty("payload", new JObject(
                               new JProperty("label", p.Label),
                               new JProperty("_id", p.Id),
                               new JProperty("id", p.Label.Split('|')[0]),
                               new JProperty("symbol", p.SymbolCode.ToLower())
                              )));

                        _publishMessage(o);
                    }
                }
            }
        }

        private void HandleModifyPosition(JObject message)
        {
            var id = (string)message["id"];
            foreach (var p in this.Positions)
            {
                if (p.Id.ToString() == id)
                {
                    var tp = message.ContainsKey("tp") ? (double?)message["tp"] : null;
                    var sl = message.ContainsKey("sl") ? (double?)message["sl"] : null;
                    if (p.TakeProfit == tp && p.StopLoss == sl)
                    {
                        JObject o = new JObject(
                          new JProperty("cmd", "modify_position_failed_result"),
                          new JProperty("_callbackId", message.ContainsKey("_callbackId") ? (string)message["_callbackId"] : null),
                          new JProperty("payload", new JObject(
                               new JProperty("error", "SL and TP are the same"),
                               new JProperty("_id", p.Id),
                               new JProperty("id", p.Label.Split('|')[0]),
                               new JProperty("symbol", p.SymbolCode.ToLower()),
                               new JProperty("sl", p.StopLoss),
                               new JProperty("tp", p.TakeProfit)
                              )
                        ));

                        _publishMessage(o);
                        return;
                    }

                    TradeResult result = ModifyPosition(p, sl, tp);

                    if (result.IsSuccessful)
                    {
                        JObject o = new JObject(
                          new JProperty("cmd", "modify_position_result"),
                          new JProperty("_callbackId", message.ContainsKey("_callbackId") ? (string)message["_callbackId"] : null),
                          new JProperty("payload", new JObject(
                               new JProperty("label", p.Label),
                               new JProperty("_id", p.Id),
                               new JProperty("id", p.Label.Split('|')[0]),
                               new JProperty("symbol", p.SymbolCode.ToLower()),
                               new JProperty("sl", result.Position.StopLoss),
                               new JProperty("tp", result.Position.TakeProfit)
                              )
                        ));

                        _publishMessage(o);
                    }
                    else
                    {
                        JObject o = new JObject(
                          new JProperty("cmd", "modify_position_failed_result"),
                          new JProperty("_callbackId", message.ContainsKey("_callbackId") ? (string)message["_callbackId"] : null),
                          new JProperty("payload", new JObject(
                               new JProperty("label", p.Label),
                               new JProperty("_id", p.Id),
                               new JProperty("id", p.Label.Split('|')[0]),
                               new JProperty("symbol", p.SymbolCode.ToLower())
                              )
                        ));

                        _publishMessage(o);
                    }
                }
            }
        }

        private void HandleCloseAllOrders(JObject msg)
        {
            var jarray = new JArray();
            var magic = (string)msg["magic"];
            foreach (var p in this.PendingOrders)
            {

                // Only cancel these orders
                if (p.Label.Contains(magic))
                {
                    var result = this.CancelPendingOrder(p);
                    jarray.Add(
                        new JObject(
                            new JProperty("cancelled", result.IsSuccessful),
                            new JProperty("label", p.Label),
                            new JProperty("_id", p.Id),
                            new JProperty("id", p.Label.Split('|')[0]),
                            new JProperty("symbol", p.SymbolCode.ToLower())
                        ));
                }
            }

            JObject o = new JObject(
                    new JProperty("cmd", "cancel_orders_all_result"),
                    new JProperty("_callbackId", msg.ContainsKey("_callbackId") ? (string)msg["_callbackId"] : null),
                    new JProperty("payload", (JArray)jarray));

            _publishMessage(o);
        }

        private void HandleCloseOrder(JObject msg)
        {
            var id = (string)msg["id"];
            foreach (var p in this.PendingOrders)
            {
                if (p.Id.ToString() == id)
                {
                    var result = this.CancelPendingOrder(p);
                    JObject o = new JObject(
                        new JProperty("cmd", "order_closed_result"),
                        new JProperty("_callbackId", msg.ContainsKey("_callbackId") ? (string)msg["_callbackId"] : null),
                        new JProperty("payload", new JObject(
                            new JProperty("cancelled", result.IsSuccessful),
                            new JProperty("label", p.Label),
                            new JProperty("_id", p.Id),
                            new JProperty("id", p.Label.Split('|')[0]),
                            new JProperty("symbol", p.SymbolCode.ToLower()))
                    ));

                    _publishMessage(o);
                }
            }
        }

        private void HandleGetPositions(JObject msg)
        {
            var positions = this.Positions.ToArray();
            if (msg.ContainsKey("label"))
            {
                if (msg.ContainsKey("symbol"))
                {
                    positions = this.Positions.FindAll((string)msg["label"], this.MarketData.GetSymbol(((string)msg["symbol"]).ToUpper()));
                }
                else
                {
                    positions = this.Positions.FindAll((string)msg["label"]);
                }
            }

            var jarray = new JArray();
            foreach (var p in positions)
            {
                jarray.Add(
                    new JObject(
                        new JProperty("label", p.Label),
                        new JProperty("_id", p.Id),
                        new JProperty("id", p.Label.Split('|')[0]),
                        new JProperty("symbol", p.SymbolCode.ToLower()),
                        new JProperty("entry_price", p.EntryPrice),
                        new JProperty("entry_time", p.EntryTime.ToUnixTime()),
                        new JProperty("pips", p.Pips)
                    ));
            }
            JObject o = new JObject(
                new JProperty("cmd", "getpositions_result"),
                new JProperty("_callbackId", msg.ContainsKey("_callbackId") ? (string)msg["_callbackId"] : null),
                new JProperty("payload", jarray));

            _publishMessage(o);
        }

        private void HandleGetOrders(JObject msg)
        {
            var orders = this.PendingOrders.AsQueryable();
            if (msg.ContainsKey("magic"))
            {
                orders = orders.Where(p => p.Label.Contains((string)msg["magic"]));
            }
            if (msg.ContainsKey("symbol"))
            {
                orders = orders.Where(p => p.SymbolCode == ((string)msg["symbol"]).ToUpper());
            }
            var jarray = new JArray();
            foreach (var p in orders)
            {
                jarray.Add(
                    new JObject(
                        new JProperty("_id", p.Id),
                        new JProperty("id", p.Label.Split('|')[0]),
                        new JProperty("symbol", p.SymbolCode.ToLower()),
                        new JProperty("price", p.TargetPrice),
                        new JProperty("lot", p.Quantity),
                        new JProperty("tp", p.TakeProfitPips),
                        new JProperty("sl", p.StopLossPips),
                        new JProperty("label", p.Label),
                        new JProperty("note", p.Comment),
                        new JProperty("type", p.TradeType.ToString().ToLower() + "_" + p.OrderType.ToString().ToLower())

                    ));
            }
            JObject o = new JObject(
               new JProperty("cmd", "get_orders_result"),
               new JProperty("_callbackId", msg.ContainsKey("_callbackId") ? (string)msg["_callbackId"] : null),
               new JProperty("payload", jarray));

            _publishMessage(o);
        }

        private void HandlePositionClosed(Position p)
        {
            JObject o = new JObject(
                   new JProperty("cmd", "position_closed_result"),
                   new JProperty("payload", new JObject(
                       new JProperty("label", p.Label),
                       new JProperty("_id", p.Id),
                       new JProperty("id", p.Label.Split('|')[0]),
                       new JProperty("symbol", p.SymbolCode.ToLower()),
                       new JProperty("pips", p.Pips),
                       new JProperty("net_profit", p.NetProfit),
                       new JProperty("gross_profit", p.GrossProfit),
                       new JProperty("ctime", DateTime.UtcNow.ToUnixTime())
                       )
               ));
            _publishMessage(o);
        }

        private void HandlePositionOpened(Position p)
        {
            JObject o = new JObject(
                   new JProperty("cmd", "position_created_result"),
                   new JProperty("payload", new JObject(
                       new JProperty("label", p.Label),
                       new JProperty("_id", p.Id),
                       new JProperty("id", p.Label.Split('|')[0]),
                       new JProperty("symbol", p.SymbolCode.ToLower()),
                       new JProperty("openprice", p.EntryPrice),
                       new JProperty("etime", p.EntryTime.ToUnixTime()),
                       new JProperty("sl", p.StopLoss),
                       new JProperty("tp", p.TakeProfit)
                       )
               ));
            _publishMessage(o);
        }

        private void HandleTradeResult(TradeResult result, JObject order)
        {
            if (result.IsSuccessful)
            {
                if (result.Position != null)
                {
                    var p = result.Position;
                    JObject o = new JObject(
                    new JProperty("cmd", "position_created_result"),
                    new JProperty("_callbackId", order.ContainsKey("_callbackId") ? (string)order["_callbackId"] : null),
                    new JProperty("payload", new JObject(
                        new JProperty("label", p.Label),
                        new JProperty("_id", p.Id),
                        new JProperty("id", p.Label.Split('|')[0]),
                        new JProperty("symbol", p.SymbolCode.ToLower()),
                        new JProperty("openprice", p.EntryPrice),
                        new JProperty("etime", p.EntryTime.ToUnixTime()),
                        new JProperty("sl", p.StopLoss),
                        new JProperty("tp", p.TakeProfit)
                        )));

                    _publishMessage(o);
                }
                else if (result.PendingOrder != null)
                {
                    JObject o = new JObject(
                        new JProperty("cmd", "order_created_result"),
                        new JProperty("_callbackId", order.ContainsKey("_callbackId") ? (string)order["_callbackId"] : null),
                        new JProperty("payload", new JObject(
                             new JProperty("label", result.PendingOrder.Label),
                             new JProperty("_id", result.PendingOrder.Id),
                             new JProperty("id", result.PendingOrder.Label.Split('|')[0])
                            )
                        ));

                    _publishMessage(o);
                }
            }
            else
            {
                JObject o = new JObject(
                        new JProperty("cmd", "order_create_failed_result"),
                        new JProperty("_callbackId", order.ContainsKey("_callbackId") ? (string)order["_callbackId"] : null),
                        new JProperty("payload", new JObject(
                             new JProperty("label", (string)order["label"])
                            )
                        ));

                _publishMessage(o);
            }
        }

        private void _publishMessage(JObject o)
        {
            o.Add(new JProperty("_timestamp", DateTime.UtcNow.ToBinary()));
            using (var redis = new RedisClient(redisHost, redisPort))
            {
                redis.Publish(order_channel_publish, o.ToString(Formatting.None));
            }
        }



        private JObject ParseMessage(string msg)
        {
            JObject o = null;
            try
            {
                o = JObject.Parse(msg);
                var hasPayload = true;
                if (o.ContainsKey("_callbackId"))
                {
                    if (!o.ContainsKey("payload"))
                    {
                        hasPayload = false;
                        o.Add("payload", new JObject());
                    }
                    ((JObject)o["payload"]).Add("_callbackId", (string)o["_callbackId"]);
                }

                var requirePayload = new System.Collections.Hashtable();
                if (hasPayload == false && requirePayloads.ContainsKey((string)o["cmd"]))
                {
                    var result = new JObject(
                       new JProperty("cmd", "invalid_message_response"),
                       new JProperty("_callbackId", o.ContainsKey("_callbackId") ? (string)o["_callbackId"] : null),
                       new JProperty("payload", new JObject(
                           new JProperty("error", string.Format("cmd requires payload: {0}", (string)o["cmd"]),
                           new JProperty("data", msg)
                           )
                       )));
                    _publishMessage(result);
                    return null;
                }
            }
            catch (Exception ex)
            {
                var result = new JObject(
                        new JProperty("cmd", "invalid_message_response"),
                        new JProperty("_callbackId", o.ContainsKey("_callbackId") ? (string)o["_callbackId"] : null),
                        new JProperty("payload", new JObject(
                            new JProperty("error", ex.Message),
                            new JProperty("source", ex.Source),
                            new JProperty("data", msg)
                            )
                        ));
                _publishMessage(result);
                return null;
            }
            return o;
        }

        private TradeResult CreateOrder(JObject order)
        {

            var symbol = this.MarketData.GetSymbol(((string)order["symbol"]).ToUpper());
            TradeResult result = null;

            var expiration = Server.Time.AddMinutes(order_expiration);
            if (order.ContainsKey("exp"))
            {
                expiration = Server.Time.AddMinutes((int)order["exp"]);
            }

            try
            {

                switch ((string)order["type"])
                {
                    case "buy_limit":
                        result = PlaceLimitOrder(
                                TradeType.Buy,
                                symbol,
                                (long)order["vol"],
                                (double)order["price"],
                                (string)order["label"],
                                (double)order["sl"],
                                (double)order["tp"],
                                expiration,
                                order.ContainsKey("comment") ? (string)order["comment"] : ""
                            );
                        break;

                    case "sell_limit":
                        result = PlaceLimitOrder(
                                TradeType.Sell,
                                symbol,
                                (long)order["vol"],
                                (double)order["price"],
                                (string)order["label"],
                                (double)order["sl"],
                                (double)order["tp"],
                                expiration,
                                order.ContainsKey("comment") ? (string)order["comment"] : ""
                            );

                        break;

                    case "buy_stop":
                        result = PlaceStopOrder(
                                TradeType.Buy,
                                symbol,
                                (long)order["vol"],
                                (double)order["price"],
                                (string)order["label"],
                                (double)order["sl"],
                                (double)order["tp"],
                                expiration,
                                order.ContainsKey("comment") ? (string)order["comment"] : ""
                            );

                        break;

                    case "sell_stop":
                        result = PlaceStopOrder(
                            TradeType.Sell,
                            symbol,
                            (long)order["vol"],
                            (double)order["price"],
                            (string)order["label"],
                            (double)order["sl"],
                            (double)order["tp"],
                            expiration,
                            order.ContainsKey("comment") ? (string)order["comment"] : ""
                        );

                        break;

                    case "sell_market":
                        result = ExecuteMarketOrder(
                            TradeType.Sell,
                            symbol,
                            (long)order["vol"],
                            (string)order["label"],
                            (double)order["sl"],
                            (double)order["tp"],
                            order.ContainsKey("range") ? (double?)order["range"] : null,
                            order.ContainsKey("comment") ? (string)order["comment"] : ""
                        );

                        try
                        {
                            alreadyHandledActions.Add(
                                _getPositionString("market_order", symbol.Code, (string)order["label"], (long)order["vol"], TradeType.Sell.ToString()), null);
                        }
                        catch (Exception ex)
                        {
                            this.Print("labels should have unique ids to prevent this: {0}", ex.Message);
                        }

                        break;

                    case "buy_market":
                        result = ExecuteMarketOrder(
                            TradeType.Buy,
                            symbol,
                            (long)order["vol"],
                            (string)order["label"],
                            (double)order["sl"],
                            (double)order["tp"],
                            order.ContainsKey("range") ? (double?)order["range"] : null,
                            order.ContainsKey("comment") ? (string)order["comment"] : ""
                        );

                        try
                        {
                            alreadyHandledActions.Add(
                               _getPositionString("market_order", symbol.Code, (string)order["label"], (long)order["vol"], TradeType.Sell.ToString()), null);
                        }
                        catch (Exception ex)
                        {
                            this.Print("labels should have unique ids to prevent this: {0}", ex.Message);
                        }

                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                this.Print("Error creating order");
                var exMessage = new JObject(
                       new JProperty("cmd", "invalid_message_response"),
                       new JProperty("_callbackId", order.ContainsKey("_callbackId") ? (string)order["_callbackId"] : null),
                       new JProperty("payload", new JObject(
                           new JProperty("error", ex.Message),
                           new JProperty("source", ex.Source),
                           new JProperty("advice", "check order parameters")
                           )
                       ));
                _publishMessage(exMessage);
                return null;
            }

            return result;
        }

        private string _getPositionString(Position p, string action)
        {
            return _getPositionString(action, p.SymbolCode, p.Label, p.Volume, p.TradeType.ToString());
        }

        private string _getPendingOrderString(PendingOrder o, string action)
        {
            return _getPositionString(action, o.SymbolCode, o.Label, o.Volume, o.TradeType.ToString());
        }

        private string _getPositionString(string action, string symbol, string label, long vol, string type)
        {
            return string.Format("{0}.{1}.{2}.{3}.{4}", symbol.ToLower(), label, vol, type, action);
        }
    }
}
