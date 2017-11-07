﻿using MessagePack.Internal;
using System;
using System.Buffers;
using System.IO;

namespace MessagePack
{
    /// <summary>
    /// High-Level API of MessagePack for C#.
    /// </summary>
    public static partial class MessagePackSerializer
    {
        static IFormatterResolver defaultResolver;

        /// <summary>
        /// FormatterResolver that used resolver less overloads. If does not set it, used StandardResolver.
        /// </summary>
        public static IFormatterResolver DefaultResolver
        {
            get
            {
                if (defaultResolver == null)
                {
                    defaultResolver = MessagePack.Resolvers.StandardResolver.Instance;
                }

                return defaultResolver;
            }
        }

        /// <summary>
        /// Is resolver decided?
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                return defaultResolver != null;
            }
        }

        /// <summary>
        /// Set default resolver of MessagePackSerializer APIs.
        /// </summary>
        /// <param name="resolver"></param>
        public static void SetDefaultResolver(IFormatterResolver resolver)
        {
            defaultResolver = resolver;
        }

        public static ArrayPool<byte> DefaultBufferPool { get; set; }
        public static int DefaultBufferPoolMinimumLength { get; set; }
        public static bool IsClearBufferWhenReturning { get; set; }

        const int ThreadStaticBufferSize = 65535;

        [ThreadStatic]
        static byte[] ThreadStaticBuffer;

        static MessagePackSerializer()
        {
            DefaultBufferPoolMinimumLength = ThreadStaticBufferSize;
            DefaultBufferPool = ArrayPool<byte>.Shared;
            IsClearBufferWhenReturning = false;
        }

        /// <summary>
        /// Serialize to binary with default resolver.
        /// </summary>
        public static byte[] Serialize<T>(T obj)
        {
            return Serialize(obj, defaultResolver);
        }

        /// <summary>
        /// Serialize to binary with specified resolver.
        /// </summary>
        public static byte[] Serialize<T>(T obj, IFormatterResolver resolver)
        {
            // use ThreadStatic path
            if (DefaultBufferPoolMinimumLength == ThreadStaticBufferSize)
            {
                var buffer = SerializeUnsafe(obj, resolver);
                return MessagePackBinary.FastCloneWithResize(buffer.Array, buffer.Count);
            }
            else
            {
                return Serialize(obj, resolver, DefaultBufferPool, DefaultBufferPoolMinimumLength);
            }
        }

        /// <summary>
        /// Serialize to binary with specified resolver and buffer pool.
        /// </summary>
        public static byte[] Serialize<T>(T obj, ArrayPool<byte> pool, int minimumLength)
        {
            return Serialize<T>(obj, defaultResolver, pool, minimumLength);
        }

        /// <summary>
        /// Serialize to binary with specified resolver and buffer pool.
        /// </summary>
        public static byte[] Serialize<T>(T obj, IFormatterResolver resolver, ArrayPool<byte> pool, int minimumLength)
        {
            if (resolver == null) resolver = DefaultResolver;
            var formatter = resolver.GetFormatterWithVerify<T>();

            var rentBuffer = pool.Rent(minimumLength);
            var buffer = rentBuffer;
            try
            {
                var len = formatter.Serialize(ref buffer, 0, obj, resolver);
                return MessagePackBinary.FastCloneWithResize(buffer, len);
            }
            finally
            {
                pool.Return(rentBuffer, IsClearBufferWhenReturning);
            }
        }

        /// <summary>
        /// Serialize to binary. Get the raw memory pool byte[]. The result can not share across thread and can not hold, so use quickly.
        /// </summary>
        public static ArraySegment<byte> SerializeUnsafe<T>(T obj)
        {
            return SerializeUnsafe(obj, defaultResolver);
        }

        /// <summary>
        /// Serialize to binary with specified resolver. Get the raw memory pool byte[]. The result can not share across thread and can not hold, so use quickly.
        /// </summary>
        [Obsolete]
        public static ArraySegment<byte> SerializeUnsafe<T>(T obj, IFormatterResolver resolver)
        {
            var bytes = ThreadStaticBuffer;
            if (bytes == null)
            {
                bytes = ThreadStaticBuffer = new byte[ThreadStaticBufferSize];
            }

            return SerializeUnsafe(obj, resolver, bytes);
        }

        /// <summary>
        /// Serialize to binary. Get the raw memory pool byte[]. The result can not share across thread and can not hold, so use quickly.
        /// </summary>
        public static ArraySegment<byte> SerializeUnsafe<T>(T obj, byte[] initialWorkingBuffer)
        {
            return SerializeUnsafe(obj, defaultResolver, initialWorkingBuffer);
        }

        /// <summary>
        /// Serialize to binary with specified resolver. Get the raw memory pool byte[]. The result can not share across thread and can not hold, so use quickly.
        /// </summary>
        public static ArraySegment<byte> SerializeUnsafe<T>(T obj, IFormatterResolver resolver, byte[] initialWorkingBuffer)
        {
            if (resolver == null) resolver = DefaultResolver;
            var formatter = resolver.GetFormatterWithVerify<T>();

            var buffer = initialWorkingBuffer;
            var len = formatter.Serialize(ref buffer, 0, obj, resolver);

            // return raw memory pool, unsafe!
            return new ArraySegment<byte>(buffer, 0, len);
        }

        /// <summary>
        /// Serialize to stream.
        /// </summary>
        public static void Serialize<T>(Stream stream, T obj)
        {
            Serialize(stream, obj, defaultResolver);
        }

        /// <summary>
        /// Serialize to stream with specified resolver.
        /// </summary>
        public static void Serialize<T>(Stream stream, T obj, IFormatterResolver resolver)
        {
            // use ThreadStatic path
            if (DefaultBufferPoolMinimumLength == ThreadStaticBufferSize)
            {
                var buffer = SerializeUnsafe(obj, resolver);
                stream.Write(buffer.Array, buffer.Offset, buffer.Count);
            }
            else
            {
                Serialize(stream, obj, resolver, DefaultBufferPool, DefaultBufferPoolMinimumLength);
            }
        }

        /// <summary>
        /// Serialize to stream with specified resolver.
        /// </summary>
        public static void Serialize<T>(Stream stream, T obj, ArrayPool<byte> pool, int minimumLength)
        {
            Serialize<T>(stream, obj, defaultResolver, pool, minimumLength);
        }

        /// <summary>
        /// Serialize to stream with specified resolver.
        /// </summary>
        public static void Serialize<T>(Stream stream, T obj, IFormatterResolver resolver, ArrayPool<byte> pool, int minimumLength)
        {
            if (resolver == null) resolver = DefaultResolver;
            var formatter = resolver.GetFormatterWithVerify<T>();

            var rentBuffer = pool.Rent(minimumLength);
            var buffer = rentBuffer;
            try
            {
                var len = formatter.Serialize(ref buffer, 0, obj, resolver);

                // do not need resize.
                stream.Write(buffer, 0, len);
            }
            finally
            {
                pool.Return(rentBuffer, IsClearBufferWhenReturning);
            }
        }

#if NETSTANDARD

        /// <summary>
        /// Serialize to stream(async).
        /// </summary>
        public static System.Threading.Tasks.Task SerializeAsync<T>(Stream stream, T obj)
        {
            return SerializeAsync(stream, obj, defaultResolver);
        }

        /// <summary>
        /// Serialize to stream(async) with specified resolver.
        /// </summary>
        public static System.Threading.Tasks.Task SerializeAsync<T>(Stream stream, T obj, IFormatterResolver resolver)
        {
            return SerializeAsync<T>(stream, obj, resolver, DefaultBufferPool, DefaultBufferPoolMinimumLength);
        }

        /// <summary>
        /// Serialize to stream(async) with specified resolver.
        /// </summary>
        public static System.Threading.Tasks.Task SerializeAsync<T>(Stream stream, T obj, ArrayPool<byte> pool, int minimumLength)
        {
            return SerializeAsync<T>(stream, obj, defaultResolver, pool, minimumLength);
        }

        /// <summary>
        /// Serialize to stream(async) with specified resolver.
        /// </summary>
        public static async System.Threading.Tasks.Task SerializeAsync<T>(Stream stream, T obj, IFormatterResolver resolver, ArrayPool<byte> pool, int minimumLength)
        {
            if (resolver == null) resolver = DefaultResolver;
            var formatter = resolver.GetFormatterWithVerify<T>();

            var rentBuffer = pool.Rent(minimumLength);
            try
            {
                var buffer = rentBuffer;
                var len = formatter.Serialize(ref buffer, 0, obj, resolver);

                // do not need resize.
                await stream.WriteAsync(buffer, 0, len).ConfigureAwait(false);
            }
            finally
            {
                pool.Return(rentBuffer, IsClearBufferWhenReturning);
            }
        }

#endif

        public static T Deserialize<T>(byte[] bytes)
        {
            return Deserialize<T>(bytes, defaultResolver);
        }

        public static T Deserialize<T>(byte[] bytes, IFormatterResolver resolver)
        {
            if (resolver == null) resolver = DefaultResolver;
            var formatter = resolver.GetFormatterWithVerify<T>();

            int readSize;
            return formatter.Deserialize(bytes, 0, resolver, out readSize);
        }

        public static T Deserialize<T>(ArraySegment<byte> bytes)
        {
            return Deserialize<T>(bytes, defaultResolver);
        }

        public static T Deserialize<T>(ArraySegment<byte> bytes, IFormatterResolver resolver)
        {
            if (resolver == null) resolver = DefaultResolver;
            var formatter = resolver.GetFormatterWithVerify<T>();

            int readSize;
            return formatter.Deserialize(bytes.Array, bytes.Offset, resolver, out readSize);
        }

        public static T Deserialize<T>(Stream stream)
        {
            return Deserialize<T>(stream, defaultResolver);
        }

        public static T Deserialize<T>(Stream stream, IFormatterResolver resolver)
        {
            return Deserialize<T>(stream, resolver, false);
        }

        public static T Deserialize<T>(Stream stream, bool readStrict)
        {
            return Deserialize<T>(stream, defaultResolver, false);
        }

        public static T Deserialize<T>(Stream stream, IFormatterResolver resolver, bool readStrict)
        {
            if (DefaultBufferPoolMinimumLength == ThreadStaticBufferSize && !readStrict)
            {
                if (resolver == null) resolver = DefaultResolver;
                var formatter = resolver.GetFormatterWithVerify<T>();

                var buffer = ThreadStaticBuffer;
                if (buffer == null)
                {
                    buffer = ThreadStaticBuffer = new byte[ThreadStaticBufferSize];
                }

                FillFromStream(stream, ref buffer);

                int readSize;
                return formatter.Deserialize(buffer, 0, resolver, out readSize);
            }
            else
            {
                return Deserialize<T>(stream, defaultResolver, readStrict, DefaultBufferPool, DefaultBufferPoolMinimumLength);
            }
        }

        public static T Deserialize<T>(Stream stream, IFormatterResolver resolver, bool readStrict, ArrayPool<byte> pool, int minimumLength)
        {
            if (resolver == null) resolver = DefaultResolver;
            var formatter = resolver.GetFormatterWithVerify<T>();

            if (!readStrict)
            {
                var rentBuffer = pool.Rent(minimumLength);
                var buffer = rentBuffer;
                try
                {

                    FillFromStream(stream, ref buffer);

                    int readSize;
                    return formatter.Deserialize(buffer, 0, resolver, out readSize);
                }
                finally
                {
                    pool.Return(rentBuffer, IsClearBufferWhenReturning);
                }
            }
            else
            {
                int _;
                var bytes = MessagePackBinary.ReadMessageBlockFromStreamUnsafe(stream, false, out _);
                int readSize;
                return formatter.Deserialize(bytes, 0, resolver, out readSize);
            }
        }

#if NETSTANDARD

        public static System.Threading.Tasks.Task<T> DeserializeAsync<T>(Stream stream)
        {
            return DeserializeAsync<T>(stream, defaultResolver);
        }

        // readStrict async read is too slow(many Task garbage) so I don't provide async option.

        public static System.Threading.Tasks.Task<T> DeserializeAsync<T>(Stream stream, IFormatterResolver resolver)
        {
            return DeserializeAsync<T>(stream, resolver, DefaultBufferPool, DefaultBufferPoolMinimumLength);
        }

        public static System.Threading.Tasks.Task<T> DeserializeAsync<T>(Stream stream, ArrayPool<byte> pool, int minimumLength)
        {
            return DeserializeAsync<T>(stream, defaultResolver, DefaultBufferPool, DefaultBufferPoolMinimumLength);
        }

        public static async System.Threading.Tasks.Task<T> DeserializeAsync<T>(Stream stream, IFormatterResolver resolver, ArrayPool<byte> pool, int minimumLength)
        {
            var rentBuffer = pool.Rent(minimumLength);
            var buf = rentBuffer;
            try
            {
                int length = 0;
                int read;
                while ((read = await stream.ReadAsync(buf, length, buf.Length - length).ConfigureAwait(false)) > 0)
                {
                    length += read;
                    if (length == buf.Length)
                    {
                        MessagePackBinary.FastResize(ref buf, length * 2);
                    }
                }

                return Deserialize<T>(buf, resolver);
            }
            finally
            {
                pool.Return(rentBuffer, IsClearBufferWhenReturning);
            }
        }

#endif

        static int FillFromStream(Stream input, ref byte[] buffer)
        {
            int length = 0;
            int read;
            while ((read = input.Read(buffer, length, buffer.Length - length)) > 0)
            {
                length += read;
                if (length == buffer.Length)
                {
                    MessagePackBinary.FastResize(ref buffer, length * 2);
                }
            }

            return length;
        }
    }
}