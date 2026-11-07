using System;

namespace BusJam
{
    public static class EventBus<T> where T : struct
    {
        private static Action<T> _handlers;
        
        public static void Subscribe(Action<T> handlers)    => _handlers += handlers;
        public static void Unsubscribe(Action<T> handlers)  => _handlers -= handlers;
        public static void Publish(T evt)                   => _handlers?.Invoke(evt);
    }
}