/*

    
    1. Copy and paste this into cAlgo,   
    2. Click on Manage References, then add the following files from this projects output folder (./src_cs/bin/Debug/)
        - CarbonFx.FOS.dll
        - csredis.dll
        - Newtonsoft.Json.dll
    3. Build!
    4. Add a chart instance in cAlgo
    5. Enable quotes for the pairs you like
    6. Start the bot! 

*/



using cAlgo.API;
using CarbonFx.FOS;

namespace cAlgo
{        
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class RedisClient : cAlgoRedisClient
    {
        [Parameter(DefaultValue = "tw_orders_in")]
        public string OrderChannelIn { get; set; }

        [Parameter(DefaultValue = "tw_orders_out")]
        public string OrderChannelOut { get; set; }

        [Parameter(DefaultValue = "tw_quote")]
        public string QuoteChannel { get; set; }

        [Parameter(DefaultValue = "localhost")]
        public string RedisHost { get; set; }

        [Parameter(DefaultValue = 6379)]
        public int RedisPort { get; set; }

        [Parameter(DefaultValue = 5)]
        public int OrderExpire { get; set; }

        [Parameter(DefaultValue = false)]
        public bool Depth { get; set; }

        [Parameter(DefaultValue = true)]
        public bool q_eurusd { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_gbpusd { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_eurgbp { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_usdjpy { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_gbpjpy { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_eurjpy { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_audusd { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_audjpy { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_chfjpy { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_usdchf { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_eurchf { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_gbpchf { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_usdcad { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_eurcad { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_euraud { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_nzdusd { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_audnzd { get; set; }

        [Parameter(DefaultValue = false)]
        public bool q_xauusd { get; set; }


        protected override void OnStart()
        {
            if (q_audjpy)
                base.symbols.Add("AUDJPY");
            if (q_audnzd)
                base.symbols.Add("AUDNZD");
            if (q_audusd)
                base.symbols.Add("AUDUSD");
            if (q_chfjpy)
                base.symbols.Add("CHFJPY");
            if (q_euraud)
                base.symbols.Add("EURAUD");
            if (q_eurcad)
                base.symbols.Add("EURCAD");
            if (q_eurchf)
                base.symbols.Add("EURCHF");
            if (q_eurgbp)
                base.symbols.Add("EURGBP");
            if (q_eurjpy)
                base.symbols.Add("EURJPY");
            if (q_eurusd)
                base.symbols.Add("EURUSD");
            if (q_gbpchf)
                base.symbols.Add("GBPCHF");
            if (q_gbpjpy)
                base.symbols.Add("GBPJPY");
            if (q_gbpusd)
                base.symbols.Add("GBPUSD");
            if (q_nzdusd)
                base.symbols.Add("NZDUSD");
            if (q_usdcad)
                base.symbols.Add("USDCAD");
            if (q_usdchf)
                base.symbols.Add("USDCHF");
            if (q_usdjpy)
                base.symbols.Add("USDJPY");
            if (q_xauusd)
                base.symbols.Add("XAUUSD");
            if (base.symbols.Count > 0)
            {
                base.Quotes = true;
            }
            base.order_channel_listen = this.OrderChannelIn;
            base.order_channel_publish = this.OrderChannelOut;
            base.quote_channel = this.QuoteChannel;
            base.order_expiration = this.OrderExpire;
            base.include_depth = this.Depth;
            base.redisHost = this.RedisHost;
            base.redisPort = this.RedisPort;
            base.OnStart();
        }
    }
}
