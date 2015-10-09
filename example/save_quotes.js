
var redis = require('redis'),
  forexData = redis.createClient(),
  redisData = redis.createClient(),
  Big = require('bignumber.js'),
  moment = require('moment');

  console.log('Capturing tick data...');

  forexData.on('error', function (err) {
      console.log('Error ' + err);
  });

  var broker_prefix = 'alpari.';

  // This should match the QuoteChannel property in the cBot
  var quoteChannel = broker_prefix + 'quote';

  forexData.subscribe(quoteChannel);

  forexData.on('message', function (channel, message) {
    var message = JSON.parse(message);
    if (channel == quoteChannel && message.cmd == 'tick'){
      var tick = message.payload;
        symbol = tick.symbol,
        bid = new Big(tick.bid),
        ask = new Big(tick.ask),
        time = moment(tick.time, 'YYYY-MM-DD HH:mm:ss.SSSZ');

      time.utcOffset(0); // SET TIME TO GMT

      var day = time.format('YYMMDD');
console.log('tick', message);
      redisData.lpush(broker_prefix+'tick.' + symbol + '.' + day, message);
    }
  });
