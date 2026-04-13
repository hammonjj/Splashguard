using System;
using System.Collections.Generic;
using UnityEngine;

namespace BitBox.Library.Eventing
{
    [DisallowMultipleComponent]
    public class MessageBusEventSender : MonoBehaviourBase
    {
        [SerializeField] private MessageBus _messageBus;
        [SerializeField] private bool _autoFindMessageBus = true;
        [SerializeField] private List<MessageBusEventEntry> _quickEvents = new List<MessageBusEventEntry>();
        [SerializeField] private List<MessageBusEventEntry> _eventList = new List<MessageBusEventEntry>();

        public MessageBus MessageBus => _messageBus;
        public bool AutoFindMessageBus => _autoFindMessageBus;
        public List<MessageBusEventEntry> QuickEvents => _quickEvents;
        public List<MessageBusEventEntry> EventList => _eventList;

        public void RefreshMessageBus()
        {
            if (_autoFindMessageBus)
            {
                _messageBus = GetComponentInParent<MessageBus>();
            }
        }

        private void Reset()
        {
            RefreshMessageBus();
        }

        private void OnValidate()
        {
            if (_autoFindMessageBus && _messageBus == null)
            {
                RefreshMessageBus();
            }
        }
    }

    [Serializable]
    public class MessageBusEventEntry
    {
        public string TypeName;
        public string FriendlyName;
        public bool Expanded = true;
        public List<MessageBusEventField> Fields = new List<MessageBusEventField>();
    }

    [Serializable]
    public class MessageBusEventField
    {
        public string FieldName;
        public string FieldTypeName;
        public int IntValue;
        public float FloatValue;
        public bool BoolValue;
        public string StringValue;
        public Vector2 Vector2Value;
        public Vector3 Vector3Value;
        public Vector4 Vector4Value;
        public Quaternion QuaternionValue;
        public Color ColorValue = Color.white;
        public UnityEngine.Object ObjectValue;
        public long LongValue;
        public double DoubleValue;
        public int EnumValue;
    }
}
