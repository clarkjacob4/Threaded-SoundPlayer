# Threaded-SoundPlayer
A simple C# experiment to thread Microsoft's SoundPlayer class

Despite my best efforts I could not get the SoundPlayer to play multiple sounds at the same time. Nevertheless, there is a lot of interesting logic in here, it's worth taking a look. Each thread runs SoundPlayer.PlaySync() which stops the thread similarly to Thread.Sleep(). The sounds are loaded directly into memory using the MemoryStream class. The threads are ran from a thread pool with queues and peeks. I tried adding a separate thread to stop sounds after a timeout expires, but you can't SoundPlayer.Stop() a sound that was ran with SoundPlayer.PlaySync(). Each action and call writes to the console for debug.
