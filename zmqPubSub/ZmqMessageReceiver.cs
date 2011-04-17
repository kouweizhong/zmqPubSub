using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Newtonsoft.Json;
using ZMQ;

namespace zmqPubSub
{
    /// <summary>
    /// A BackgroundWorker that listens in a loop for messages 
    /// coming in on a zmq SUB Socket  
    /// </summary>
    public class ZmqMessageReceiver : BackgroundWorker, IReceiveMessages
    {
        Context _messagingContext;
        ISubject<object> _messages;

        public string ListenAddress { get; private set; }
        public Encoding MessageEncoding { get; private set; }

        protected ZmqMessageReceiver()
        {
            WorkerSupportsCancellation = true;
            WorkerReportsProgress = true;
        }

        public ZmqMessageReceiver(Context messagingContext, string listenAddress, Encoding messageEncoding) : this()
        {
            _messagingContext = messagingContext;
            ListenAddress = listenAddress;
            MessageEncoding = messageEncoding;
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            using (var incoming = _messagingContext.Socket(SocketType.SUB))
            {
                incoming.Subscribe("", MessageEncoding);
                incoming.Connect(ListenAddress);

                while (!CancellationPending)
                {
                    var messageBytes = incoming.Recv();
                    if(messageBytes == null) continue;
                    var jsonMessage = MessageEncoding.GetString(messageBytes);
                    var message = JsonConvert.DeserializeObject(jsonMessage);
                    ReportProgress(0, message);
                }

                incoming.Unsubscribe("", MessageEncoding);
                e.Cancel = true;
            }
        }

        protected override void OnProgressChanged(ProgressChangedEventArgs e)
        {
            if(_messages != null)
                _messages.OnNext(e.UserState);
        }

        protected override void OnRunWorkerCompleted(RunWorkerCompletedEventArgs e)
        {
            _messages = null;
        }

        public void ListenForMessages(ISubject<object> messages)
        {
            _messages = messages;
            RunWorkerAsync();
        }

        public void StopListeningForMessages()
        {
            CancelAsync();
        }

        public bool IsListening
        {
            get { return IsBusy; }
        }
    }
}