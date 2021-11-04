using System;
using System.Windows.Forms;
using System.Media;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Collections.Concurrent;

public class spThreadClass
{
    public Thread spThread;
    public SoundPlayer spPlayer;
    public volatile bool spWhile; //dispose variable
    public int spIndex; //bug tracking only
    public volatile string spVariable; //current track name
    public volatile Stopwatch spStopwatch;

    public spThreadClass(Thread spThread, SoundPlayer spPlayer, int tIndex)
    {
        this.spThread = spThread;
        this.spPlayer = spPlayer;
        this.spWhile = true;
        this.spIndex = tIndex;
        this.spVariable = "";
        this.spStopwatch = new Stopwatch();
    }
}

public struct spListStruct
{
    public MemoryStream spStream;
    public long spLength; //tLength: Each sound has a configurable timeout variable, after this time a new sound can be queued.
    
    public spListStruct(MemoryStream spStream, long spLength)
    {
        this.spStream = spStream;
        this.spLength = spLength;
    }
}

/**************************************************************************************************/
/*                                                                                                */
/*                                        Audio Player                                            */
/*                                                                                                */
/**************************************************************************************************/
//https://stackoverflow.com/questions/22208258/how-to-play-sounds-asynchronuously-but-themselves-in-a-queue
//https://stackoverflow.com/questions/51215385/how-to-play-multiple-sounds-at-once-in-c-sharp
//SoundPlayer does not support playing multiple sounds at the same time, even on different threads


public class cAudioPlayer
{
    public ConcurrentQueue<string> spQueuePeekOnly = new ConcurrentQueue<string>(); //Queue
    public BlockingCollection<string> spQueue; //Queue, for peeking only
    public Dictionary<string, spListStruct> spList = new Dictionary<string, spListStruct>(); //List of sounds
    public volatile List<spThreadClass> spThreads = new List<spThreadClass>(); //Thread pool
    public int spThreadNumber; //Number of threads in pool
    public volatile bool spWhile = true; //Sound timeout thread dispose variable
    
    public void sInitialize(int tThreadNumber)
    {
        spQueue = new BlockingCollection<string>(spQueuePeekOnly);
        spThreadNumber = tThreadNumber;
        for (int tIndex = 0; tIndex < spThreadNumber; tIndex++)
        {
            int tTemp = tIndex;
            spThreads.Add(new spThreadClass(new Thread(() => sStepAsync(tTemp)), new SoundPlayer(), tIndex));
            spThreads[tIndex].spThread.IsBackground = true;
            spThreads[tIndex].spThread.Start();
        }
        Thread sThread = new Thread(() => sStepMainAsync());
        sThread.Start();
    }
    public void sDestroy()
    {
        Console.WriteLine("Sound timeout thread close requested");
        spWhile = false;
        for (int tIndex = 0; tIndex < spThreadNumber; tIndex++)
        {
            Console.WriteLine("Thread x/" + tIndex + " close requested");
            spThreads[tIndex].spWhile = false; //Tells the threads to close
            spThreads[tIndex].spPlayer.Dispose(); //Clears the object from memory on next garbage collector run
        }
    }
    public void sLoad(string tFile, string tVariable, long tLength)
    {
        //Loads the file into memory, ready to be streamed into the audio player anytime
        Console.WriteLine("Sound " + tVariable + " added with length " + tLength.ToString());
        spList[tVariable] = new spListStruct(new MemoryStream(File.ReadAllBytes(tFile)), tLength); //Don't need to dispose these
    }
    public void sStepMainAsync()
    {
        string ManagedThreadId = Thread.CurrentThread.ManagedThreadId.ToString();
        Console.WriteLine("Sound timeout thread " + ManagedThreadId + "/0 started");
        while (spWhile) //Main loop
        {
            for (int tIndex = 0; tIndex < spThreadNumber; tIndex++) //Run through each thread
            {
                string tVariable = spThreads[tIndex].spVariable;
                if (tVariable != "") //If the thread is playing a sound
                {
                    long spStopwatchMilliseconds = spThreads[tIndex].spStopwatch.ElapsedMilliseconds;
                    long tVariablespLength = spList[tVariable].spLength;
                    if (spStopwatchMilliseconds >= tVariablespLength) //If the sound reached the custom max length
                    {
                        Console.WriteLine("Sound " + tVariable + " stop requested on thread x/" + tIndex.ToString() +
                            " due to length limit reached (" + spStopwatchMilliseconds.ToString() + " / " + tVariablespLength.ToString() + "ms)");
                        spThreads[tIndex].spPlayer.Stop(); //Attempt to stop the sound
                    }
                }
            }
            Thread.Sleep(20);
        }
        Console.WriteLine("Sound timeout thread " + ManagedThreadId + "/0 closing");
    }
    public void sStepAsync(int tIndex)
    {
        string ManagedThreadId = Thread.CurrentThread.ManagedThreadId.ToString();
        Console.WriteLine("Thread " + ManagedThreadId + "/" + tIndex.ToString() + " started");
        if (tIndex == spThreads[tIndex].spIndex) //Verify thread ID, maybe not necessary. Basically verifies the spThreads list order
        {
            while (spThreads[tIndex].spWhile) //Main loop
            {
                string tVariable = "";
                while (spThreads[tIndex].spWhile && (!spQueue.TryTake(out tVariable))) //true if an item could be removed; otherwise, false.
                {
			        Thread.Sleep(20);
                }
                if (spThreads[tIndex].spWhile) //Thread told to close
                {
                    int spQueueCount = spQueue.Count; //Queue count sometimes inaccurate if threads execute their take actions milliseconds apart
                    spThreads[tIndex].spVariable = tVariable;
                    spThreads[tIndex].spPlayer.Stream = spList[tVariable].spStream;
                    spThreads[tIndex].spStopwatch.Restart(); //Play duration
                    Console.WriteLine("Sound " + tVariable + " started on thread " + ManagedThreadId + "/" + tIndex.ToString() +
                        ", queue is now " + (spQueueCount == 0 ? "empty" : "at " + spQueueCount.ToString()));
                    spThreads[tIndex].spPlayer.PlaySync(); //Plays similar to Thread.Sleep
                    Console.WriteLine("Sound " + tVariable + " stopped on thread " + ManagedThreadId + "/" + tIndex.ToString() +
                        " (" + spThreads[tIndex].spStopwatch.ElapsedMilliseconds.ToString() + "ms)");
                    spThreads[tIndex].spVariable = "";
                    spThreads[tIndex].spPlayer.Stream = null;
                }
			    Thread.Sleep(20);
            }
            Console.WriteLine("Thread " + ManagedThreadId + "/" + tIndex.ToString() + " closing");
        }
        else //Thread ID verify failed
        {
            Console.WriteLine("Thread " + ManagedThreadId + 
                "/x failed to verify index x/" + tIndex.ToString() + " vs x/" + spThreads[tIndex].spIndex.ToString());
        }
    }
    public void sQueue(string tVariable)
    {
        Thread sThread = new Thread(() => sQueueAsync(tVariable));
        sThread.Start();
    }
    public void sQueueAsync(string tVariable)
    {
        bool bIsPlaying = false;
        for (int tIndex = 0; tIndex < spThreadNumber; tIndex++)
        {
            if (tVariable.Equals(spThreads[tIndex].spVariable))
            {
                Console.WriteLine("Queue: Sound " + tVariable + " is already playing on thread x/" + tIndex.ToString());
                bIsPlaying = true; //The sound is already playing
            }
        }

        if (!bIsPlaying)
        {
            if (spQueue.Count > 0) //If there are items in the queue
            {
                string tVariablePeek = "";
                Stopwatch swStopwatch = new Stopwatch(); //timeout counter
                swStopwatch.Start();
                bool swWhileTimeout = true;
                bool swWhileCount = true;
                while (swWhileTimeout && swWhileCount && (!spQueuePeekOnly.TryPeek(out tVariablePeek)))
                {
                    //Retry peeks for 1 second, or until count is 0
                    //Peeked item could be gone by the end of the operation, which would mean the sound started playing
                    if (swStopwatch.ElapsedMilliseconds >= 1000L) //1 second peek timeout
                    {
                        Console.WriteLine("Queue: Peek for sound " + tVariable + " has timed out");
                        swWhileTimeout = false;
                    }
                    if (spQueue.Count == 0) //The queue became empty since the TryPeek loop started
                    {
                        Console.WriteLine("Queue: Peek for sound " + tVariable + ", queue has emptied");
                        swWhileCount = false;
                    }
                    Thread.Sleep(20);
                } //End loop
                int spQueueCount = swWhileCount ? spQueue.Count : 0;
                if (swWhileTimeout && //If the 1 second timeout hasn't elapsed
                    ((tVariablePeek != tVariable) || !swWhileCount) && //If the next item in the queue is different
                    (spQueueCount < 5)) //Max 5 items in queue
                {
                    Console.WriteLine("Queue: Enqueuing sound " + tVariable + ", queue was "
                        + (spQueueCount == 0 ? "empty" : "at " + spQueueCount.ToString() + ", peeked sound " + tVariablePeek));
                    spQueue.Add(tVariable);
                }
            }
            else //Queue is empty
            {
                Console.WriteLine("Queue: Enqueuing sound " + tVariable + ", queue was empty");
                spQueue.Add(tVariable); //Add the sound to the empty queue
            }
        }
    }
}

namespace WindowsFormsApp4
{
    public partial class Form1 : Form
    {
        cAudioPlayer myAudioPlayer = new cAudioPlayer();
        public Form1()
        {
            InitializeComponent();
            this.FormClosed += new FormClosedEventHandler(Form1_FormClosed);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            myAudioPlayer.sInitialize(4);
            myAudioPlayer.sLoad(@"C:\Users\jclark\Downloads\mixkit-electronics-power-up-2602.wav","power",5000);
            myAudioPlayer.sLoad(@"C:\Users\jclark\Downloads\mixkit-futuristic-technology-room-ambience-2503.wav","room",5000);
            myAudioPlayer.sLoad(@"C:\Users\jclark\Downloads\mixkit-heavy-storm-rain-loop-2400.wav","rain",5000);
            myAudioPlayer.sLoad(@"C:\Users\jclark\Downloads\mixkit-positive-interface-beep-221.wav","beep",5000);

            myAudioPlayer.sQueue("power");
            myAudioPlayer.sQueue("beep");
        }

        private void Form1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            myAudioPlayer.sDestroy();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            myAudioPlayer.sDestroy();
        }
    }
}
