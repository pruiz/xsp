//
// Mono.WebServer.LingeringNetworkStream
//
// Authors:
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//
// (C) Copyright 2004 Novell, Inc. (http://www.novell.com)
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

using System;
using System.Net.Sockets;
using Mono.WebServer.Log;

namespace Mono.WebServer
{
	public class LingeringNetworkStream : NetworkStream 
	{
		static readonly int USECONDS_TO_LINGER = 2000000;
		static readonly long MAX_USECONDS_TO_LINGER = 30000000;
		// We dont actually use the data from this buffer. So we cache it...
		static byte [] buffer;

		static LingeringNetworkStream()
		{
			var wait = Environment.GetEnvironmentVariable("XSP_LINGER_WAIT");
			var maxwait = Environment.GetEnvironmentVariable("XSP_LINGER_WAIT_MAX");

			USECONDS_TO_LINGER = string.IsNullOrWhiteSpace(wait) ? 2000000 : Convert.ToInt32(wait);
			MAX_USECONDS_TO_LINGER = string.IsNullOrWhiteSpace(maxwait) ? 30000000 : Convert.ToInt64(maxwait);
		}

		public LingeringNetworkStream (Socket sock, bool owns) : base (sock, owns)
		{
			EnableLingering = true;
			OwnsSocket = owns;
		}

		public bool OwnsSocket { get; private set; }

		public bool EnableLingering { get; set; }

		void LingeringClose ()
		{
			long waited = 0;

			if (!Connected)
				return;

			try {
				Socket.Shutdown (SocketShutdown.Send);
				DateTime start = DateTime.UtcNow;
				while (waited < MAX_USECONDS_TO_LINGER) {
					int nread = 0;
					try {
						if (!Socket.Poll (USECONDS_TO_LINGER, SelectMode.SelectRead)) {
							Logger.Write (LogLevel.Warning, "LongeringClose: TimedOut while polling for socket data.");
							continue;
						}

						if (buffer == null)
							buffer = new byte [512];

						nread = Socket.Receive (buffer, 0, buffer.Length, 0);
					} catch { }

					if (nread == 0)
						break;

					waited = (long) (DateTime.UtcNow - start).TotalMilliseconds * 1000;
				}
			} catch {
				// ignore - we don't care, we're closing anyway
			}
		}

		public override void Close ()
		{
			if (EnableLingering) {
				try {
					LingeringClose ();
				} finally {
					base.Close ();
				}
			}
			else
				base.Close ();
		}

		public bool Connected {
			get { return Socket.Connected; }
		}
	}
}
