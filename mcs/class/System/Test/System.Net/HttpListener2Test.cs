//
// HttpListener2Test.cs
//	- Unit tests for System.Net.HttpListener - connection testing
//
// Author:
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
#if NET_2_0
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NUnit.Framework;

// ***************************************************************************************
// NOTE: when adding prefixes, make then unique per test, as MS might take 'some time' to
// unregister it even after explicitly closing the listener.
// ***************************************************************************************
namespace MonoTests.System.Net {
	[TestFixture]
#if TARGET_JVM	
	[Ignore ("The class HttpListener is not supported")]
#endif
	public class HttpListener2Test {
		public class MyNetworkStream : NetworkStream {
			public MyNetworkStream (Socket sock) : base (sock, true)
			{
			}

			public Socket GetSocket ()
			{
				return Socket;
			}
		}

		public static HttpListener CreateAndStartListener (string prefix)
		{
			HttpListener listener = new HttpListener ();
			listener.Prefixes.Add (prefix);
			listener.Start ();
			return listener;
		}

		public static MyNetworkStream CreateNS (int port)
		{
			Socket sock = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			sock.Connect (new IPEndPoint (IPAddress.Loopback, port));
			return new MyNetworkStream (sock);
		}

		public static void Send (Stream stream, string str)
		{
			byte [] bytes = Encoding.ASCII.GetBytes (str);
			stream.Write (bytes, 0, bytes.Length);
		}

		public static string Receive (Stream stream, int size)
		{
			byte [] bytes = new byte [size];
			int nread = stream.Read (bytes, 0, size);
			return Encoding.ASCII.GetString (bytes, 0, nread);
		}

		public static string ReceiveWithTimeout (Stream stream, int size, int timeout, out bool timed_out)
		{
			byte [] bytes = new byte [size];
			IAsyncResult ares = stream.BeginRead (bytes, 0, size, null, null);
			timed_out = !ares.AsyncWaitHandle.WaitOne (timeout, false);
			if (timed_out)
				return null;
			int nread = stream.EndRead (ares);
			return Encoding.ASCII.GetString (bytes, 0, nread);
		}

		public static HttpListenerContext GetContextWithTimeout (HttpListener listener, int timeout, out bool timed_out)
		{
			IAsyncResult ares = listener.BeginGetContext (null, null);
			timed_out = !ares.AsyncWaitHandle.WaitOne (timeout, false);
			if (timed_out)
				return null;
			return listener.EndGetContext (ares);
		}

		[Test]
		public void Test1 ()
		{
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test1/");
			NetworkStream ns = CreateNS (9000);
			Send (ns, "GET / HTTP/1.1\r\n\r\n"); // No host
			string response = Receive (ns, 512);
			ns.Close ();
			listener.Close ();
			Assert.IsTrue (response.StartsWith ("HTTP/1.1 400"));
		}

		[Test]
		public void Test2 ()
		{
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test2/");
			NetworkStream ns = CreateNS (9000);
			Send (ns, "GET / HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n"); // no prefix
			string response = Receive (ns, 512);
			ns.Close ();
			listener.Close ();
			Assert.IsTrue (response.StartsWith ("HTTP/1.1 400"));
		}

		[Test]
		public void Test3 ()
		{
			StringBuilder bad = new StringBuilder ();
			for (int i = 0; i < 33; i++){
				if (i != 13)
					bad.Append ((char) i);
			}
			bad.Append ('(');
			bad.Append (')');
			bad.Append ('[');
			bad.Append (']');
			bad.Append ('<');
			bad.Append ('>');
			bad.Append ('@');
			bad.Append (',');
			bad.Append (';');
			bad.Append (':');
			bad.Append ('\\');
			bad.Append ('"');
			bad.Append ('/');
			bad.Append ('?');
			bad.Append ('=');
			bad.Append ('{');
			bad.Append ('}');

			foreach (char b in bad.ToString ()){
				HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test3/");
				NetworkStream ns = CreateNS (9000);
				Send (ns, String.Format ("MA{0} / HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n", b)); // bad method
				
				string response = Receive (ns, 512);
				ns.Close ();
				listener.Close ();
				Console.WriteLine ("Response is: {0}", response);
				Assert.AreEqual (true, response.StartsWith ("HTTP/1.1 400"), String.Format ("Failed on {0}", (int) b));
			}
		}

		[Test]
		public void Test4 ()
		{
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test4/");
			NetworkStream ns = CreateNS (9000);
			Send (ns, "POST /test4/ HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n"); // length required
			string response = Receive (ns, 512);
			ns.Close ();
			listener.Close ();
			Assert.IsTrue (response.StartsWith ("HTTP/1.1 411"));
		}

		[Test]
		public void Test5 ()
		{
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test5/");
			NetworkStream ns = CreateNS (9000);
			Send (ns, "POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nTransfer-Encoding: pepe\r\n\r\n"); // not implemented
			string response = Receive (ns, 512);
			ns.Close ();
			listener.Close ();
			Assert.IsTrue (response.StartsWith ("HTTP/1.1 501"));
		}

		[Test]
		public void Test6 ()
		{
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test6/");
			NetworkStream ns = CreateNS (9000);
			 // not implemented! This is against the RFC. Should be a bad request/length required
			Send (ns, "POST /test6/ HTTP/1.1\r\nHost: 127.0.0.1\r\nTransfer-Encoding: identity\r\n\r\n");
			string response = Receive (ns, 512);
			ns.Close ();
			listener.Close ();
			Assert.IsTrue (response.StartsWith ("HTTP/1.1 501"));
		}

		[Test]
		public void Test7 ()
		{
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test7/");
			NetworkStream ns = CreateNS (9000);
			Send (ns, "POST /test7/ HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 3\r\n\r\n123");
			HttpListenerContext ctx = listener.GetContext ();
			Send (ctx.Response.OutputStream, "%%%OK%%%");
			string response = Receive (ns, 1024);
			ns.Close ();
			listener.Close ();
			Assert.IsTrue (response.StartsWith ("HTTP/1.1 200"));
			Assert.IsTrue (-1 != response.IndexOf ("Transfer-Encoding: chunked"));
		}

		[Test]
		public void Test8 ()
		{
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test8/");
			NetworkStream ns = CreateNS (9000);
			// Just like Test7, but 1.0
			Send (ns, "POST /test8/ HTTP/1.0\r\nHost: 127.0.0.1\r\nContent-Length: 3\r\n\r\n123");
			HttpListenerContext ctx = listener.GetContext ();
			Send (ctx.Response.OutputStream, "%%%OK%%%");
			string response = Receive (ns, 512);
			ns.Close ();
			listener.Close ();
			Assert.IsTrue (response.StartsWith ("HTTP/1.1 200"));
			Assert.IsTrue (-1 == response.IndexOf ("Transfer-Encoding: chunked"));
		}

		[Test]
		public void Test9 ()
		{
			// 1.0 + "Transfer-Encoding: chunked"
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test9/");
			NetworkStream ns = CreateNS (9000);
			Send (ns, "POST /test9/ HTTP/1.0\r\nHost: 127.0.0.1\r\nTransfer-Encoding: chunked\r\n\r\n3\r\n123\r\n0\r\n\r\n");
			bool timeout;
			string response = ReceiveWithTimeout (ns, 512, 1000, out timeout);
			Assert.IsFalse (timeout);
			listener.Close ();
			ns.Close ();
			Assert.IsTrue (response.StartsWith ("HTTP/1.1 411"));
		}

		[Test]
		public void Test10 ()
		{
			// Same as Test9, but now we shutdown the socket for sending.
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test10/");
			MyNetworkStream ns = CreateNS (9000);
			Send (ns, "POST /test10/ HTTP/1.0\r\nHost: 127.0.0.1\r\nTransfer-Encoding: chunked\r\n\r\n3\r\n123\r\n0\r\n\r\n");
			ns.GetSocket ().Shutdown (SocketShutdown.Send);
			bool timeout;
			string response = ReceiveWithTimeout (ns, 512, 1000, out timeout);
			Assert.IsFalse (timeout);
			listener.Close ();
			ns.Close ();
			Assert.IsTrue (response.StartsWith ("HTTP/1.1 411"));
		}

		[Test]
		public void Test11 ()
		{
			// 0.9
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test11/");
			MyNetworkStream ns = CreateNS (9000);
			Send (ns, "POST /test11/ HTTP/0.9\r\nHost: 127.0.0.1\r\n\r\n123");
			ns.GetSocket ().Shutdown (SocketShutdown.Send);
			string input = Receive (ns, 512);
			ns.Close ();
			listener.Close ();
			Assert.IsTrue (input.StartsWith ("HTTP/1.1 400"));
		}

		[Test]
		public void Test12 ()
		{
			// 0.9
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test12/");
			MyNetworkStream ns = CreateNS (9000);
			Send (ns, "POST /test12/ HTTP/0.9\r\nHost: 127.0.0.1\r\nContent-Length: 3\r\n\r\n123");
			ns.GetSocket ().Shutdown (SocketShutdown.Send);
			string input = Receive (ns, 512);
			ns.Close ();
			listener.Close ();
			Assert.IsTrue (input.StartsWith ("HTTP/1.1 400"));
		}

		[Test]
		public void Test13 ()
		{
			// 0.9
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test13/");
			MyNetworkStream ns = CreateNS (9000);
			Send (ns, "GEt /test13/ HTTP/0.9\r\nHost: 127.0.0.1\r\n\r\n");
			ns.GetSocket ().Shutdown (SocketShutdown.Send);
			string input = Receive (ns, 512);
			ns.Close ();
			listener.Close ();
			Assert.IsTrue (input.StartsWith ("HTTP/1.1 400"));
		}

		HttpListenerRequest test14_request;
		ManualResetEvent test_evt;
		bool test14_error;
		[Test]
		public void Test14 ()
		{
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test14/");
			MyNetworkStream ns = CreateNS (9000);
			Send (ns, "POST /test14/ HTTP/1.0\r\nHost: 127.0.0.1\r\nContent-Length: 3\r\n\r\n123");
			HttpListenerContext c = listener.GetContext ();
			test14_request = c.Request;
			test_evt = new ManualResetEvent (false);
			Thread thread = new Thread (ReadToEnd);
			thread.Start ();
			if (test_evt.WaitOne (3000, false) == false) {
				thread.Abort ();
				test_evt.Close ();
				Assert.IsTrue (false, "Timed out");
			}
			test_evt.Close ();
			Assert.AreEqual ("123", read_to_end, "Did not get the expected input.");
			c.Response.Close ();
			ns.Close ();
		}

		string read_to_end;
		void ReadToEnd ()
		{
			using (StreamReader r = new StreamReader (test14_request.InputStream)) {
				read_to_end = r.ReadToEnd ();
				test_evt.Set ();
			}
		}

		[Test]
		public void Test15 ()
		{
			// 2 separate writes -> 2 packets. Body size > 8kB
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test15/");
			MyNetworkStream ns = CreateNS (9000);
			Send (ns, "POST /test15/ HTTP/1.0\r\nHost: 127.0.0.1\r\nContent-Length: 8888\r\n\r\n");
			Thread.Sleep (800);
			string data = new string ('a', 8888);
			Send (ns, data);
			HttpListenerContext c = listener.GetContext ();
			HttpListenerRequest req = c.Request;
			using (StreamReader r = new StreamReader (req.InputStream)) {
				read_to_end = r.ReadToEnd ();
			}
			Assert.AreEqual (read_to_end.Length, data.Length, "Wrong length");
			Assert.IsTrue (data == read_to_end, "Wrong data");
			c.Response.Close ();
			ns.Close ();
		}

		[Test]
		public void Test16 ()
		{
			// 1 single write with headers + body (size > 8kB)
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test16/");
			MyNetworkStream ns = CreateNS (9000);
			StringBuilder sb = new StringBuilder ();
			sb.Append ("POST /test16/ HTTP/1.0\r\nHost: 127.0.0.1\r\nContent-Length: 8888\r\n\r\n");
			string eights = new string ('b', 8888);
			sb.Append (eights);
			string data = sb.ToString ();
			Send (ns, data);
			HttpListenerContext c = listener.GetContext ();
			HttpListenerRequest req = c.Request;
			using (StreamReader r = new StreamReader (req.InputStream)) {
				read_to_end = r.ReadToEnd ();
			}
			Assert.AreEqual (read_to_end.Length, read_to_end.Length, "Wrong length");
			Assert.IsTrue (eights == read_to_end, "Wrong data");
			c.Response.Close ();
			ns.Close ();
		}

		[Test]
		public void Test17 ()
		{
			HttpListener listener = CreateAndStartListener ("http://127.0.0.1:9000/test17/");
			NetworkStream ns = CreateNS (9000);
			Send (ns, "RANDOM /test17/ HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 3\r\n\r\n123");
			HttpListenerContext ctx = listener.GetContext ();
			Send (ctx.Response.OutputStream, "%%%OK%%%");
			string response = Receive (ns, 1024);
			ns.Close ();
			listener.Close ();
			Assert.IsTrue (response.StartsWith ("HTTP/1.1 200"));
			Assert.IsTrue (-1 != response.IndexOf ("Transfer-Encoding: chunked"));
		}
	}

	[TestFixture]
	public class HttpListenerBugs {
		[Test]
		public void TestNonChunkedAsync ()
		{
			HttpListener listener = HttpListener2Test.CreateAndStartListener ("http://127.0.0.1:9123/");

			listener.BeginGetContext (callback, listener);
			
			HttpListener2Test.MyNetworkStream ns = HttpListener2Test.CreateNS (9123);
			string message = "<script>\n"+
				" <!-- register the blueprint for our show-headers service -->\n"+
				" <action verb=\"POST\" path=\"/host/register\">\n" +
				"    <blueprint>\n" +
				"      <assembly>dream.tutorial.show-headers</assembly>\n" +
				"      <class>MindTouch.Dream.Tutorial.ShowHeadersService</class>\n" +
				"    </blueprint>\n" +
				"  </action>\n" +
				"\n" +
				"  <!-- instantiate it -->\n" +
				"  <action verb=\"POST\" path=\"/host/start\">\n" +
				"    <config>\n" +
				"      <path>show-headers</path>\n" +
				"      <class>MindTouch.Dream.Tutorial.ShowHeadersService</class>\n" +
				"    </config>\n" +
				"  </action>\n" +
				"</script>";
			string s = String.Format ("POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: {0}\r\n\r\n{1}",
						message.Length, message);  
			HttpListener2Test.Send (ns, s);
			bool timedout;
			string response = HttpListener2Test.ReceiveWithTimeout (ns, 1024, 3000, out timedout);
			ns.Close ();
			Assert.IsFalse (timedout);
		}

		void callback (IAsyncResult ar)
		{
			HttpListener l = (HttpListener) ar.AsyncState;

			HttpListenerContext c = l.EndGetContext (ar);
			HttpListenerRequest request = c.Request;

			StreamReader r = new StreamReader (request.InputStream);
			string sr =r.ReadToEnd ();
			HttpListener2Test.Send (c.Response.OutputStream, "Miguel is love");
		}
	}
}
#endif

