var util = require("util");
var Promise = require('bluebird');
var redis = require("redis");
var events = require("events");
var moment = require('moment');
var Hashids = require('hashids');
var hashids = new Hashids("fxmpcbid:"+moment().valueOf(), 8, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890");
var id = 0;

function _getCallbackId(){
  return hashids.encode(++id);
}
function _RedisChannelCallbackWrapper(listenChannel, publishChannel){
    var self = this;
    this.channelPublish = publishChannel;
    this.channelListen = listenChannel;
    this.redisClient = redis.createClient();
    this.redisClientChannel = redis.createClient();
    this.callbackIds = {};
    this.redisClientChannel.subscribe(this.channelListen);
    this.redisClientChannel.on("message", function (channel, message) {
      if (channel == self.channelListen){
        message = JSON.parse(message);
        if (message._callbackId){
          var id = message._callbackId;
          var promise = self.callbackIds[id];
          if (typeof promise !== 'undefined'){
            delete message._callbackId;
            promise.accept(message);
            if (typeof promise.cb !== 'undefined'){
              promise.cb(message);
            }
            delete self.callbackIds[id];
          }
          // How can we reject anything if the reject
          // can't be identified with
          // the initiator?
        } else {
            self.emit('unhandled', message);
        }
      }
    });
    this.sendCmd = function(payload, cb){
      var self = this;
        return new Promise(function(resolve, reject){
          var id = _getCallbackId();
          payload['_callbackId'] = id;
          self.callbackIds[id] = { accept: resolve, reject: reject, cb: cb};
          self.redisClient.publish(self.channelPublish, JSON.stringify(payload));
        });
    }
}

util.inherits(_RedisChannelCallbackWrapper, events.EventEmitter);

module.exports = _RedisChannelCallbackWrapper;
