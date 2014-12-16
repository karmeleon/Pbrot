Pbrot
=====

A somewhate functional Buddhabrot renderer using OpenMP and OpenCL. If you just want the binaries, check the accordingly-named Release folders and download them. You'll probably need Visual C++ 2013. CLbrot requires an OpenCL-capable GPU with the appropriate OpenCL runtime installed.

If you want to compile it yourself, open the .sln in VS2013+. You'll need to modify the CLbrot project to include your OpenCL SDK directories if you're not using the CUDA SDK like I did. Instructions on that are [here](http://kode-stuff.blogspot.com/2012/11/setting-up-opencl-in-visual-studio_1.html).

CLbrot will also cause most graphics drivers to "crash" after two seconds because of a feature called TDR. You'll want to disable it to run the app at all; just make the registry key(s) described [here](http://msdn.microsoft.com/en-us/library/windows/hardware/ff569918%28v=vs.85%29.aspx) and reboot.
