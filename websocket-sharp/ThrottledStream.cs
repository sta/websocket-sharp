using System;
using System.IO;
using System.Threading;

namespace WebSocketSharp {

	public class ThrottledStream : Stream {

		/// <summary>
		/// Actual base stream the data is passed to
		/// </summary>
		private Stream _objStream;

		/// <summary>
		/// Maximum number of bytes per second that's being allowed
		/// 0 = infinite
		/// </summary>
		private long _lngMaxBytesPerSecond = 0;
		
		/// <summary>
		/// Global byte counter used to calculate the current speed (in combination with _lngStartTime)
		/// </summary>
		private long _lngByteCount;

		/// <summary>
		/// Start time the current speed is being calculated on (in combination with _lngByteCount)
		/// </summary>
		private long _lngStartTime;

		#region Constructors
		/// <summary>
		/// Creates the stream without any bandwidth throttling
		/// </summary>
		/// <param name="pStream"></param>
		public ThrottledStream(Stream pStream) : this(pStream, 0) { }

		/// <summary>
		/// Creates the stream with the given maximum bytes per second
		/// </summary>
		/// <param name="pStream"></param>
		/// <param name="pBytesPerSecond"></param>
		public ThrottledStream(Stream pStream, long pBytesPerSecond) {
			_objStream = pStream;
			_lngMaxBytesPerSecond = pBytesPerSecond;
			_lngByteCount = 0;
			_lngStartTime = Environment.TickCount;
		}
		#endregion

		#region Public Properties
		/// <summary>
		/// Maximum number of bytes per second allowed to be sent
		/// </summary>
		public long MaxBytesPerSecond {
			get { return _lngMaxBytesPerSecond; }
			set {
				if (value < 0) { _lngMaxBytesPerSecond = 0; }
				_lngMaxBytesPerSecond = value;
				Reset(); // -- reset current counter
			}
		}

		/// <summary>
		/// Current bytes per second being sent over the stream
		/// </summary>
		public long CurrentBytesPerSecond {
			get { return (_lngByteCount * 1000L) / (Environment.TickCount - _lngStartTime); }
		}
		#endregion

		#region Stream Overrides
		public override bool CanRead { get { return _objStream.CanRead; } }
		public override bool CanSeek { get { return _objStream.CanSeek; } }
		public override bool CanWrite { get { return _objStream.CanWrite; } }
		public override long Length { get { return _objStream.Length; } }
		public override void Flush() { _objStream.Flush(); }
		public override long Position {
			get { return _objStream.Position; }
			set { _objStream.Position = value; }
		}
		public override long Seek(long pOffset, SeekOrigin pOrigin) {
			return _objStream.Seek(pOffset, pOrigin);
		}
		public override void SetLength(long value) {
			_objStream.SetLength(value);
		}
		public override string ToString() {
			return _objStream.ToString();
		}
		public override int Read(byte[] pBuffer, int pOffset, int pCount) {
			WaitIfNeeded(pCount);
			return _objStream.Read(pBuffer, pOffset, pCount);
		}
		public override void Write(byte[] buffer, int offset, int count) {
			WaitIfNeeded(count);
			_objStream.Write(buffer, offset, count);
		}
		#endregion

		/// <summary>
		/// Slows down sending the bytes if needed
		/// </summary>
		/// <param name="pByteCount"></param>
		private void WaitIfNeeded(int pByteCount) {
			if(_lngMaxBytesPerSecond == 0) { return; } // -- no limit
			if (pByteCount == 0) { return; } // -- nothing to write, so no waiting

			//update global byte counter
			_lngByteCount += pByteCount;

			var lngTimePassed = Environment.TickCount - _lngStartTime;
			if (lngTimePassed > 0) {
				var lngCurrentSpeed = _lngByteCount * 1000L / lngTimePassed;
				if (lngCurrentSpeed > _lngMaxBytesPerSecond) { // -- do we need to wait?
					var intMilisecondsToSleep = ((_lngByteCount * 1000L / _lngMaxBytesPerSecond) - lngTimePassed);
					if (intMilisecondsToSleep > 1) {
						try {
							Thread.Sleep((int)intMilisecondsToSleep);
						} catch (Exception) { } // can happen when threads get killed/aborted
						Reset();
					}
				}
			}
		}

		/// <summary>
		/// Reset the timer being used to throttle the speed
		/// </summary>
		protected void Reset() {
			//To better shape the stream, keep 10 second history
			if ((Environment.TickCount - _lngStartTime) > 10000) {
				_lngByteCount = 0;
				_lngStartTime = Environment.TickCount;
			}
		}
	}
}