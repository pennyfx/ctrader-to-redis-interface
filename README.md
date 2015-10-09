# cTrader/cAlgo Redis Interface

** This software is alpha, may have bugs, may not work as advertised and may result in financial losses.  Use at your own risk.  **

Tired of being bound to the language of the Fx platform your using?  Want to create an external service that has nothing to do with cAlgo?  

CarbonFx cAlgo Redis Interface allows complete control over your trading account using pub/sub Redis channels that speak JSON.

This project is for experienced programmers who love the idea of writing EVERYTHING from scratch.



# Install

**Prerequisites**

[Visual Studio Community Edition](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx)  **FREE**  

[cAlgo/cTrader via Tradersway](http://www.tradersway.com/trading_platforms/ctrader)

[Redis](https://github.com/MSOpenTech/redis/releases) for Windows

[Nodejs](https://nodejs.org/en/download/) (required for tests)




## Setup

#### System

1. Install Redis with default settings.  
2. Install Nodejs
3. Install Visual Studio
4. Install cTrader/cAlgo and create a demo account.  (You probably already have this if you're interested in this project in the first place)

#### Build project in Visual Studio

1. Open solution in Visual Studio
2. Add reference to your `cAlgo.dll`
```
C:\Users\[YOU]\Documents\cAlgo\API\cAlgo.API.dll
```
3. Make sure these dependencies are added
	- `Newtonsoft.JSON` library  (NuGet Package)
	- `CSRedis`  (NuGet Package)
4. Build

#### Build bot in cAlgo

1. Open cAlgo  (Please make sure it's a **DEMO** account!!)
2. Create a new bot
3. Copy and paste `src_cs/cAlgoRedisClientBot.cs` into it
4. Click on Manage References, then add the following files from this projects output folder (`./src_cs/bin/Debug/`)

				- CarbonFx.FOS.dll
        - csredis.dll
        - Newtonsoft.Json.dll
5. Build!  (in cAlgo)
6. Add a chart instance to it
7. Enable quotes for the pairs you like, IF you want to use cAlgo for quotes too
8. Start the bot!

#####  Run tests

At this point everything should be working correctly, but to make sure, lets run the tests.

If you don't have `mocha` installed globally yet, run:
```shell
npm install mocha -g
```

Once that's finished, run:
```
$ mocha


  calgo redis Interface
    √ should connect to redis
    √ should get server time in unix format
    √ should get return error if no payload is passed
    create and cancel orders
      √ should create sell_limit order (202ms)
      √ should create buy_limit order (222ms)
      √ should create sell_stop order (238ms)
      √ should create buy_stop order (246ms)
    has orders
      √ should get orders
      √ should get orders by magic
      √ should get orders by symbol
      √ should get orders by symbol and magic
    market order
      √ should create market buy and receive a position back (589ms)
      √ should create market sell and receive a position back (520ms)
      √ should modify SL on position (226ms)
      √ should modify TP on position (249ms)


  15 passing (7s)

```

The test suite is not complete, but it's probably the most important part of the project.  If I have to create a new interface for Metatrader4, NinjaTrader, etc, I should be able to point this test suite at it and see if it is working correctly.

# Features

- Basic order/position management
- Quotes + market depth

##### TODO:
- Account info
- Historical Data (maybe)

# Motivation

I started this project because I don't like locking my strategies to a particular platform. Don't get me wrong, I like cTrader/cAlgo.  It's a lot better software than Metatrader4, but I wanted to be free to create anything I wanted without restrictions.  Plus, who knows how much longer I'll be with a broker that offers cAlgo.   I don't want to be SOL, if I have to switch.

In addition to this project, I also have an order manager, backtester, optimizer and a web interface that pulls these pieces together.  I'm not ready to open-source them yet, but it's on my list of things to do.  They are all written in Nodejs.  Let me know if you're interested, maybe I can share some code.  


# LICENSE

MIT
