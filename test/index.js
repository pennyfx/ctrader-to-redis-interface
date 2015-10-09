var assert = require('chai').assert;
var Redis = require('../src_js/redis_channel_wrapper.js');
// this should match the channels used
var channel = new Redis('test_out', 'test_in');
var async = require('async');

describe('calgo redis Interface', function(){
    it('should connect to redis', function(done){
      channel.redisClient.ping(function(err, res){
        assert.isNull(err);
        assert.equal(res, 'PONG');
        done();
      })
    });
    it('should get server time in unix format', function(done){
      channel.sendCmd({
        cmd: 'get_time'
      }).then(function(message){
        assert.equal(message.cmd, 'get_time_result');
        assert.isNumber(message.payload.time);
        done();
      })
    });
    it('should get return error if no payload is passed', function(done){
      channel.sendCmd({
        cmd: 'create_order'
      }).then(function(message){
        assert.equal(message.cmd, 'invalid_message_response');
        done();
      })
    });
    describe('create and cancel orders', function(){
      var orderIds = [];
      var label = 'testing1';
      after(function(done){
        async.each(orderIds, function(orderId, cb){
          channel.sendCmd({
            cmd: 'cancel_order',
            payload:{
              id: orderId
            }
          }).then(function(message){
            assert.equal(message.cmd, 'order_closed_result');
            assert.isTrue(message.payload.cancelled);
            assert.isNumber(message.payload._id);
            cb();
          })
        }, done);
      });
      it('should create sell_limit order', function(done){
        var comment = 'does this even work?';
        channel.sendCmd({
          cmd: 'create_order',
          payload: {
            type: 'sell_limit',
            vol: 10000,
            symbol: 'eurusd',
            price: 1.90,
            tp: 100,
            sl: 100,
            label: label,
            comment: comment
          }
        }).then(function(message){
          assert.equal(message.cmd, 'order_created_result');
          assert.isNumber(message.payload._id);
          assert.equal(message.payload.id, label);
          orderIds.push(message.payload._id);
          done();
        })
      })
      it('should create buy_limit order', function(done){
        var comment = 'does this even work?';
        channel.sendCmd({
          cmd: 'create_order',
          payload: {
            type: 'buy_limit',
            vol: 10000,
            symbol: 'eurusd',
            price: 0.90,
            tp: 100,
            sl: 100,
            label: label,
            comment: comment
          }
        }).then(function(message){
          assert.equal(message.cmd, 'order_created_result');
          assert.isNumber(message.payload._id);
          assert.equal(message.payload.id, label);
          orderIds.push(message.payload._id);
          done();
        })
      })
      it('should create sell_stop order', function(done){
        var comment = 'does this even work?';
        channel.sendCmd({
          cmd: 'create_order',
          payload: {
            type: 'sell_stop',
            vol: 10000,
            symbol: 'eurusd',
            price: 0.90,
            tp: 100,
            sl: 100,
            label: label,
            comment: comment
          }
        }).then(function(message){
          assert.equal(message.cmd, 'order_created_result');
          assert.isNumber(message.payload._id);
          assert.equal(message.payload.id, label);
          orderIds.push(message.payload._id);
          done();
        })
      })
      it('should create buy_stop order', function(done){
        var comment = 'does this even work?';
        channel.sendCmd({
          cmd: 'create_order',
          payload: {
            type: 'buy_stop',
            vol: 10000,
            symbol: 'eurusd',
            price: 1.90,
            tp: 100,
            sl: 100,
            label: label,
            comment: comment
          }
        }).then(function(message){
          assert.equal(message.cmd, 'order_created_result');
          assert.isNumber(message.payload._id);
          assert.equal(message.payload.id, label);
          orderIds.push(message.payload._id);
          done();
        })
      })

    });
    describe('has orders', function(){
      var orderIds = [];
      var magic1 = 'xyz';
      var magic2 = 'zyx';
      before(function(done){
        async.parallel([
          function(cb){
            channel.sendCmd({
              cmd: 'create_order',
              payload: {
                type: 'buy_limit',
                vol: 10000,
                symbol: 'eurusd',
                price: 0.90,
                tp: 100,
                sl: 100,
                label: magic1
              }
            }).then(function(message){
              assert.equal(message.cmd, 'order_created_result');
              orderIds.push(message.payload._id);
              cb();
            })
        }, function(cb){
          channel.sendCmd({
            cmd: 'create_order',
            payload: {
              type: 'buy_limit',
              vol: 10000,
              symbol: 'gbpusd',
              price: 0.90,
              tp: 100,
              sl: 100,
              label: magic2
            }
          }).then(function(message){
            assert.equal(message.cmd, 'order_created_result');
            orderIds.push(message.payload._id);
            cb();
          })
        },
        function(cb){
          channel.sendCmd({
            cmd: 'create_order',
            payload: {
              type: 'buy_limit',
              vol: 10000,
              symbol: 'gbpusd',
              price: 0.85,
              tp: 100,
              sl: 100,
              label: magic1
            }
          }).then(function(message){
            assert.equal(message.cmd, 'order_created_result');
            orderIds.push(message.payload._id);
            cb();
          })
        }
        ], function(){
          done();
        })
      });
      after(function(done){
        async.each(orderIds, function(orderId, cb){
          channel.sendCmd({
            cmd: 'cancel_order',
            payload:{
              id: orderId
            }
          }).then(function(message){
            assert.equal(message.cmd, 'order_closed_result');
            cb();
          })
        }, done);
      });
      it('should get orders', function(done){
        channel.sendCmd({
          cmd: 'get_orders'
        }).then(function(message){
          assert.equal(message.cmd, 'get_orders_result');
          assert.isArray(message.payload);
          assert.equal(message.payload.length, 3);
          done();
        })
      })
      it('should get orders by magic', function(done){
        channel.sendCmd({
          cmd: 'get_orders',
          payload: {
            magic: magic1
          }
        }).then(function(message){
          assert.equal(message.cmd, 'get_orders_result');
          assert.isArray(message.payload);
          assert.equal(message.payload.length, 2);
          message.payload.forEach(function(order){
            assert.equal(order.label, magic1);
          });
          done();
        })
      })
      it('should get orders by symbol', function(done){
        channel.sendCmd({
          cmd: 'get_orders',
          payload: {
            symbol: 'gbpusd'
          }
        }).then(function(message){
          assert.equal(message.cmd, 'get_orders_result');
          assert.isArray(message.payload);
          assert.equal(message.payload.length, 2);
          message.payload.forEach(function(order){
            assert.equal(order.symbol, 'gbpusd');
          });
          done();
        })
      })
      it('should get orders by symbol and magic', function(done){
        channel.sendCmd({
          cmd: 'get_orders',
          payload: {
            symbol: 'gbpusd',
            magic: magic1
          }
        }).then(function(message){
          assert.equal(message.cmd, 'get_orders_result');
          assert.isArray(message.payload);
          assert.equal(message.payload.length, 1);
          message.payload.forEach(function(order){
            assert.equal(order.symbol, 'gbpusd');
            assert.equal(order.label, magic1);
          });
          done();
        })
      })
    });
    describe('market order', function(){
      var positionIds = [];
      var modifyMeId;
      before(function(done){
        channel.sendCmd({
          cmd: 'create_order',
          payload: {
            type: 'buy_market',
            vol: 1000,
            symbol: 'eurusd',
            tp: 100,
            sl: 100,
            label:'test3'
          }
        }).then(function(message){
          assert.equal(message.cmd, 'position_created_result');
          positionIds.push(message.payload._id);
          modifyMeId = message.payload._id;
          done();
        })
      });
      after(function(done){
        async.each(positionIds, function(id, cb){
          channel.sendCmd({
            cmd: 'close_position',
            payload:{
              id: id
            }
          }).then(function(message){
            assert.equal(message.cmd, 'position_closed_result');
            cb();
          })
        }, done);
      });
      it('should create market buy and receive a position back', function(done){
        channel.sendCmd({
          cmd: 'create_order',
          payload: {
            type: 'buy_market',
            vol: 1000,
            symbol: 'eurusd',
            tp: 100,
            sl: 100,
            label:'test2'
          }
        }).then(function(message){
          assert.equal(message.cmd, 'position_created_result');
          positionIds.push(message.payload._id);
          done();
        })
      })
      it('should create market sell and receive a position back', function(done){
        channel.sendCmd({
          cmd: 'create_order',
          payload: {
            type: 'sell_market',
            vol: 1000,
            symbol: 'gbpusd',
            tp: 100,
            sl: 100,
            label:'test1'
          }
        }).then(function(message){
          assert.equal(message.cmd, 'position_created_result');
          positionIds.push(message.payload._id);
          done();
        })
      })
      it('should modify SL on position', function(done){
        channel.sendCmd({
          cmd: 'modify_position',
          payload: {
            id: modifyMeId,
            sl: 1.0000
          }
        }).then(function(message){
          assert.equal(message.cmd, 'modify_position_result');
          assert.equal(message.payload.sl, 1.0000);
          done();
        })
      })
      it('should modify TP on position', function(done){
        channel.sendCmd({
          cmd: 'modify_position',
          payload: {
            id: modifyMeId,
            tp: 2.0000
          }
        }).then(function(message){
          assert.equal(message.cmd, 'modify_position_result');
          assert.equal(message.payload.tp, 2.0000);
          done();
        })
      })
    })
})
