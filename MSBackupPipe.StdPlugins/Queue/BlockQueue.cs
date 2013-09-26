using System;
using System.Collections.Generic;

///-------------------------------------------------------------------------------------------------
//	namespace:	PlanetaryDBLib.Collections
//
//	summary:	PlanteryDB Library Collections namespace. A collection of useful tools.
//
///-------------------------------------------------------------------------------------------------
namespace PlanetaryDBLib.Collections
{
    ///-------------------------------------------------------------------------------------------------
    /// <summary>	Queue of blocks. </summary>
    ///
    /// <remarks>	Wes Brown, 5/26/2009. </remarks>
    ///-------------------------------------------------------------------------------------------------
    internal class BlockQueue
    {
        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="BlockQueue" /> class. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///-------------------------------------------------------------------------------------------------
// ReSharper disable UnusedMember.Global
        public BlockQueue() : this(1024*1024, true)
// ReSharper restore UnusedMember.Global
        {
        }

        //allow caller to specifiy the queue size and expanding or not

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Constructor. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="queueSize">	Size of the queue. </param>
        /// <param name="expanding">	true to expanding. </param>
        ///-------------------------------------------------------------------------------------------------
        public BlockQueue(int queueSize, bool expanding)
        {
            //initalize all holding variables

            //intialize the size holders
            _fixedQueueSize = Size = queueSize;

            Expandable = expanding;
            
            //initalize queues based on expanding or not
            if (expanding) return;

            //initalize the pointers
            Head = 0;
            Tail = 0;
            Used = 0;

            //build inital queues
            _fixedQueue = new byte[Size];
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	 the size. </summary>
        ///
        /// <value>	The size. </value>
        ///-------------------------------------------------------------------------------------------------
// ReSharper disable MemberCanBePrivate.Global
        public int Size { get; private set; }
// ReSharper restore MemberCanBePrivate.Global

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	 the head. </summary>
        ///
        /// <value>	The head. </value>
        ///-------------------------------------------------------------------------------------------------
// ReSharper disable MemberCanBePrivate.Global
        public int Head { get; private set; }
// ReSharper restore MemberCanBePrivate.Global

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	 the tail. </summary>
        ///
        /// <value>	The tail. </value>
        ///-------------------------------------------------------------------------------------------------
// ReSharper disable MemberCanBePrivate.Global
        public int Tail { get; private set; }
// ReSharper restore MemberCanBePrivate.Global

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Gets the free. </summary>
        ///
        /// <value>	The free. </value>
        ///-------------------------------------------------------------------------------------------------
        public int Free
        {
            get { return _fixedQueueSize - Used; }
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	 the used. </summary>
        ///
        /// <value>	The used. </value>
        ///-------------------------------------------------------------------------------------------------
        public int Used { get; private set; }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	 a value indicating whether. </summary>
        ///
        /// <value>	true if expandable, false if not. </value>
        ///-------------------------------------------------------------------------------------------------
// ReSharper disable MemberCanBePrivate.Global
        public bool Expandable { get; private set; }
// ReSharper restore MemberCanBePrivate.Global

        #region internal variables

        //fixed queues for block opperations

        //is an expanding queue or not
        private readonly Queue<byte> _expandingQueue;

        private readonly byte[] _fixedQueue;

        //pointer trackers

        //size trackers
        //inital queue size for fixed queue
        private readonly int _fixedQueueSize;

        //initial queue size for expanding queue
        private int _fixedQueueRemander;

        #endregion

        //allocate a default of 1MB for total queue size.
        //default is an expanding queue.

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Puts. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <exception cref="BlockQueueException">			Thrown when block queue. </exception>
        /// <exception cref="BlockFullQueueException">		Thrown when block full queue. </exception>
        /// <exception cref="BlockOverwriteQueueException">	Thrown when block overwrite queue. </exception>
        ///
        /// <param name="array">		[in,out] the ref byte to put. </param>
        /// <param name="offset">		[in,out] the ref int to put. </param>
        /// <param name="count">		[in,out] number of. </param>
        /// <param name="returnCount">	[out] number of returns. </param>
        ///-------------------------------------------------------------------------------------------------
        public void Put(ref byte[] array, ref int offset, ref int count, out int returnCount)
        {
            //we are an expanding array this method isn't allowed
            if (Expandable)
            {
                throw new BlockQueueException("Cannot use EnqueBlock in expanding queue mode.");
            }

            //if the amount of data being pushed in is bigger than the available space
            //throw error
            if (count > Size - Used)
            {
                throw new BlockFullQueueException("Not enough space in queue.");
            }

            //split end write
            if (Tail + count > _fixedQueueSize && Head < Tail)
            {
                //you could comment this out or I may make a flag to allow 
                //tail overwrites. The reason I don't is you could overwrite just part of a 
                //block then get funny reads going forward.
                //if that is the case you may not want to take the hit for a split write 
                //and just copy the block to the front of the array, loosing available space
                //in the process
                if (Head > Tail && (Tail + count) > Head && Tail > 0 && Head > 0)
                {
                    throw new BlockOverwriteQueueException("Head overwrite detected.");
                }
                try
                {
                    //find the differential for the split process.
                    _fixedQueueRemander = (_fixedQueueSize - Tail);

                    //head overwrite due to tail rap
                    if (_fixedQueueRemander > Head)
                    {
                        throw new BlockOverwriteQueueException("Head overwrite detected.");
                    }
                    //fill end of queue
                    Buffer.BlockCopy(array, offset, _fixedQueue, Tail, _fixedQueueRemander);
                    //fill beginning of queue
                    Buffer.BlockCopy(array, offset + _fixedQueueRemander, _fixedQueue, 0,
                                     count - _fixedQueueRemander);
                    //set new end to the amount of data copied to beginning fo queue
                    Tail = count - _fixedQueueRemander;
                    //increase the fill factor of queue
                    Used += count;
                }
                catch (Exception ex)
                {
                    //on failure throw a general error
                    throw new BlockQueueException("Error occurred copying to buffer.", ex);
                }
            }
            else
            {
                if (Tail + count > Head && Tail > 0 && Head > 0)
                {
                    throw new BlockOverwriteQueueException("Head overwrite detected.");
                }

                try
                {
                    //do normal block copy
                    Buffer.BlockCopy(array, offset, _fixedQueue, Tail, count);
                    //move the tail forward
                    Tail += count;
                    //increase the fill factor of queue
                    Used += count;
                }
                catch (Exception ex)
                {
                    //on failure throw a general error
                    throw new BlockQueueException("Error occurred copying to BlockQueue.", ex);
                }
            }

            //return the number of bytes written
            returnCount = count;
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Gets. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <exception cref="BlockQueueException">	Thrown when block queue. </exception>
        ///
        /// <param name="array">		[in,out] the ref byte to get. </param>
        /// <param name="offset">		[in,out] the ref int to get. </param>
        /// <param name="count">		[in,out] number of. </param>
        /// <param name="returnCount">	[out] number of returns. </param>
        ///-------------------------------------------------------------------------------------------------
        public void Get(ref byte[] array, ref int offset, ref int count, out int returnCount)
        {
            //expaning queue isn't allowed to call this method
            if (Expandable)
                throw new BlockQueueException("Cannot use DequeBlock in expanding queue mode.");
            if (count > array.Length)
                throw new BlockQueueException("Trying to read more data than target array can hold");

            //check to see if we are over reading.
            //this is important since we can get dirty data 
            //back to the calling method
            //could throw an error here but I'm using this to stream data
            //an may request an array fill that won't get a full payload.
            if (count > Used)
                //reset the count to the available amount of data
                count = Used;

            if (!(count == 0))
            {
                //do we still have data to read after the count reset?
                //detect if we have moved the start ahead of the end due to a split write
                //if the start will rap again we have a split read.
                if ((Head > Tail) && (Head + count > _fixedQueueSize))
                {
                    try
                    {
                        //calculate how much will fit at the end of the buffer
                        _fixedQueueRemander = (_fixedQueueSize - Head);
                        //fill the end of the array
                        Buffer.BlockCopy(_fixedQueue, Head, array, offset, _fixedQueueRemander);
                        //copy remander to front of array
                        Buffer.BlockCopy(_fixedQueue, 0, array, offset + _fixedQueueRemander,
                                         count - _fixedQueueRemander);
                        //set our new start point at the front of the array
                        Head = count - _fixedQueueRemander;
                        //decement our fill
                        Used -= count;

                        //this actually prevents uneeded splits a regular circular buffer
                        //doesn't suffer from block splits since it can't happen
                        if (Head == Tail)
                            Tail = Head = 0;
                    }
                    catch (Exception ex)
                    {
                        //dunno what happened but it must be bad :)
                        throw new BlockQueueException("Error occurred copying to buffer.", ex);
                    }
                }

                    //check to see if we are reading past the end of the queue
                    //I'm not sure if this is needed gotta do some additional debugging first.
                else if ((count <= Used))
                {
                    //copy from queue to array
                    try
                    {
                        Buffer.BlockCopy(_fixedQueue, Head, array, offset, count);
                        Head += count;
                        Used -= count;
                    }
                    catch (Exception ex)
                    {
                        throw offset + count < array.Length
                                  ? new BlockQueueException(
                                        "Return buffer overflow count + offset > return buffer size.\n" + ex.Message)
                                  : new BlockQueueException("Error occurred copying to buffer.\n" + ex.Message);
                    }
                }
                    //technially we should never hit this error but I'm paranoid about implementeting this queue 
                else
                {
                    throw new BlockQueueException("Error read past available data.\n");
                }
            }
            returnCount = count;

            //you guessed it somone requested to read more data from the queue than the target array
            //can physically hold. 
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Clears this object to its blank/initial state. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///-------------------------------------------------------------------------------------------------
        public void Clear()
        {
            //reset all pointers
            Head = Tail = _fixedQueueRemander = Used = 0;
        }

        #region not emplmented yet
        /*
        public int Put(byte[] array, int offset, int count)
        {
            return count;
        }

        public int Get(byte[] array, int offset, int count)
        {
            return count;
        }
*/
// ReSharper disable UnusedMember.Global
        public void EPut(byte[] array, int offset, int count)
// ReSharper restore UnusedMember.Global
        {
            if (!Expandable)
            {
                throw new BlockQueueException("Cannot use EPut in non-expanding queue mode.");
            }

            for (int index = 0; index < count; index++)

                _expandingQueue.Enqueue(array[index + offset]);
        }

// ReSharper disable UnusedMember.Global
        public int EGet(byte[] array, int offset, int count)
// ReSharper restore UnusedMember.Global
        {
            if (!Expandable)
            {
                throw new BlockQueueException("Cannot use EGet in non-expanding queue mode.");
            }

            int index;
            for (index = 0; index < count && _expandingQueue.Count > 0; index++)
                array[index + offset] = _expandingQueue.Dequeue();
            return index;
        }

        #endregion
    }

    ///-------------------------------------------------------------------------------------------------
    /// <summary>	Exception for signalling block queue errors. </summary>
    ///
    /// <remarks>	Wes Brown, 5/26/2009. </remarks>
    ///-------------------------------------------------------------------------------------------------
    internal sealed class BlockQueueException : Exception
    {
        // Use the default ApplicationException constructors

/*
        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///-------------------------------------------------------------------------------------------------
        public BlockQueueException()
        {
        }
*/

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class with a
        /// 			specified error message. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="s">	The string. </param>
        ///
#pragma warning disable 1574
        /// ### <param name="message">	The message that describes the error. </param>
#pragma warning restore 1574
        ///-------------------------------------------------------------------------------------------------
        public BlockQueueException(string s) : base(s)
        {
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class with a
        /// 			specified error message and a reference to the inner exception that is the cause
        /// 			of this exception. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="s">	The string. </param>
        /// <param name="ex">	The ex. </param>
        ///
#pragma warning disable 1574
        /// ### <param name="message">			The error message that explains the reason for the exceptio
#pragma warning restore 1574
        /// 									n. </param>
#pragma warning disable 1574
        /// ### <param name="innerException">	The exception that is the cause of the current exception,
#pragma warning restore 1574
        /// 									or a null reference (Nothing in Visual Basic) if no inner
        /// 									exception is specified. </param>
        ///-------------------------------------------------------------------------------------------------
        public BlockQueueException(string s, Exception ex) : base(s, ex)
        {
        }
    }

    ///-------------------------------------------------------------------------------------------------
    /// <summary>	Exception for signalling block empty queue errors. </summary>
    ///
    /// <remarks>	Wes Brown, 5/26/2009. </remarks>
    ///-------------------------------------------------------------------------------------------------
    internal abstract class BlockEmptyQueueException : Exception
    {
        // Use the default ApplicationException constructors

/*
        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///-------------------------------------------------------------------------------------------------
        protected BlockEmptyQueueException()
        {
        }
*/

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class with a
        /// 			specified error message. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="s">	The string. </param>
        ///
#pragma warning disable 1574
        /// ### <param name="message">	The message that describes the error. </param>
#pragma warning restore 1574
/*
        ///-------------------------------------------------------------------------------------------------
        public BlockEmptyQueueException(string s) : base(s)
        {
        }
*/

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class with a
        /// 			specified error message and a reference to the inner exception that is the cause
        /// 			of this exception. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="s">	The string. </param>
        /// <param name="ex">	The ex. </param>
        ///
#pragma warning disable 1574
        /// ### <param name="message">			The error message that explains the reason for the exceptio
#pragma warning restore 1574
        /// 									n. </param>
#pragma warning disable 1574
        /// ### <param name="innerException">	The exception that is the cause of the current exception,
#pragma warning restore 1574
        /// 									or a null reference (Nothing in Visual Basic) if no inner
        /// 									exception is specified. </param>
        ///-------------------------------------------------------------------------------------------------
// ReSharper disable PublicConstructorInAbstractClass
        public BlockEmptyQueueException(string s, Exception ex) : base(s, ex)
// ReSharper restore PublicConstructorInAbstractClass
        {
        }
    }

    ///-------------------------------------------------------------------------------------------------
    /// <summary>	Exception for signalling block full queue errors. </summary>
    ///
    /// <remarks>	Wes Brown, 5/26/2009. </remarks>
    ///-------------------------------------------------------------------------------------------------
    internal sealed class BlockFullQueueException : Exception
    {
        // Use the default ApplicationException constructors

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///-------------------------------------------------------------------------------------------------
// ReSharper disable UnusedMember.Global
        public BlockFullQueueException()
// ReSharper restore UnusedMember.Global
        {
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class with a
        /// 			specified error message. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="s">	The string. </param>
        ///
#pragma warning disable 1574
        /// ### <param name="message">	The message that describes the error. </param>
#pragma warning restore 1574
        ///-------------------------------------------------------------------------------------------------
        public BlockFullQueueException(string s) : base(s)
        {
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class with a
        /// 			specified error message and a reference to the inner exception that is the cause
        /// 			of this exception. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="s">	The string. </param>
        /// <param name="ex">	The ex. </param>
        ///
        /// ### <param name="message">			The error message that explains the reason for the exceptio
        /// 									n. </param>
#pragma warning disable 1574
        /// ### <param name="innerException">	The exception that is the cause of the current exception,
#pragma warning restore 1574
        /// 									or a null reference (Nothing in Visual Basic) if no inner
        /// 									exception is specified. </param>
        ///-------------------------------------------------------------------------------------------------
// ReSharper disable UnusedMember.Global
        public BlockFullQueueException(string s, Exception ex) : base(s, ex)
// ReSharper restore UnusedMember.Global
        {
        }
    }

    ///-------------------------------------------------------------------------------------------------
    /// <summary>	Exception for signalling block overwrite queue errors. </summary>
    ///
    /// <remarks>	Wes Brown, 5/26/2009. </remarks>
    ///-------------------------------------------------------------------------------------------------
    internal sealed class BlockOverwriteQueueException : Exception
    {
        // Use the default ApplicationException constructors
// ReSharper disable UnusedMember.Global
        public BlockOverwriteQueueException()
// ReSharper restore UnusedMember.Global
        {
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class with a
        /// 			specified error message. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="s">	The string. </param>
        ///
#pragma warning disable 1574
        /// ### <param name="message">	The message that describes the error. </param>
#pragma warning restore 1574
        ///-------------------------------------------------------------------------------------------------
        public BlockOverwriteQueueException(string s) : base(s)
        {
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class with a
        /// 			specified error message and a reference to the inner exception that is the cause
        /// 			of this exception. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="s">	The string. </param>
        /// <param name="ex">	The ex. </param>
        ///
#pragma warning disable 1574
        /// ### <param name="message">			The error message that explains the reason for the exceptio
#pragma warning restore 1574
        /// 									n. </param>
#pragma warning disable 1574
        /// ### <param name="innerException">	The exception that is the cause of the current exception,
#pragma warning restore 1574
        /// 									or a null reference (Nothing in Visual Basic) if no inner
        /// 									exception is specified. </param>
        ///-------------------------------------------------------------------------------------------------
// ReSharper disable UnusedMember.Global
        public BlockOverwriteQueueException(string s, Exception ex) : base(s, ex)
// ReSharper restore UnusedMember.Global
        {
        }
    }

    ///-------------------------------------------------------------------------------------------------
    /// <summary>	Exception for signalling block empty request queue errors. </summary>
    ///
    /// <remarks>	Wes Brown, 5/26/2009. </remarks>
    ///-------------------------------------------------------------------------------------------------
// ReSharper disable UnusedMember.Global
    internal sealed class BlockEmptyRequestQueueException : Exception
// ReSharper restore UnusedMember.Global
    {
        // Use the default ApplicationException constructors

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///-------------------------------------------------------------------------------------------------
        public BlockEmptyRequestQueueException()
        {
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class with a
        /// 			specified error message. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="s">	The string. </param>
        ///
#pragma warning disable 1574
        /// ### <param name="message">	The message that describes the error. </param>
#pragma warning restore 1574
        ///-------------------------------------------------------------------------------------------------
        public BlockEmptyRequestQueueException(string s) : base(s)
        {
        }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>	Initializes a new instance of the <see cref="T:System.Exception" /> class with a
        /// 			specified error message and a reference to the inner exception that is the cause
        /// 			of this exception. </summary>
        ///
        /// <remarks>	Wes Brown, 5/26/2009. </remarks>
        ///
        /// <param name="s">	The string. </param>
        /// <param name="ex">	The ex. </param>
        ///
#pragma warning disable 1574
        /// ### <param name="message">			The error message that explains the reason for the exceptio
#pragma warning restore 1574
        /// 									n. </param>
#pragma warning disable 1574
        /// ### <param name="innerException">	The exception that is the cause of the current exception,
#pragma warning restore 1574
        /// 									or a null reference (Nothing in Visual Basic) if no inner
        /// 									exception is specified. </param>
        ///-------------------------------------------------------------------------------------------------
        public BlockEmptyRequestQueueException(string s, Exception ex) : base(s, ex)
        {
        }
    }
}