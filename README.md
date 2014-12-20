#Pbrot

A mostly-functional Buddhabrot renderer using OpenMP and OpenCL. If you just want the binaries, check the accordingly-named Release folders and download them in either the 32 or 64-bit flavors. If it gives missing DLL errors, install Visual C++ 2013 and .NET 4.5. The OpenCL renderer requires a CPU or GPU with the appropriate OpenCL runtime installed.

If you want to compile it yourself, open the .sln in VS2013+. You'll need to modify the libpbrot project to include your OpenCL SDK directories if you're not using the CUDA SDK like I did. Instructions on that are [here](http://kode-stuff.blogspot.com/2012/11/setting-up-opencl-in-visual-studio_1.html).

The OpenCL renderer will also cause most graphics drivers to "crash" after two seconds when running on the system's main GPU because of a feature called TDR. You'll want to disable it to run the app at all; just make the registry key(s) described [here](http://msdn.microsoft.com/en-us/library/windows/hardware/ff569918%28v=vs.85%29.aspx) and reboot.

Uses the excellent [SimpleCL](https://code.google.com/p/simple-opencl/) library.

###Sample


![Sample](http://i.imgur.com/Ze8sIUS.jpg)
![Sample](http://i.imgur.com/xm8J9Dm.jpg)
