# ACEPatcher
A simple to use, gui based program for patching .NET assemblies

![ACE patcher](https://i.ibb.co/wCB6pvx/Ace-Patcher.png)

## Features

* Patches a wide range of methods(static methods, instance methods, constructors, getters, setters, etc.)
* Works on packed assemblies
* Works on a majority of obfuscators(including but not limited to:DnGuard,Eazfuscator,VMProtect,.NET reactor,Babel,Crypto Obfuscator and many more)
* Easy and intuative GUI
* Exports patches
* Exports patches with password
* Imports patches

## How to write patch methods

Firstly you have to analyze the target assembly and see which methods do you want to patch, once you found the methods you want to patch open a new visual studio c# project and write patch methods after you have written patch methods compile the assembly with Release build.

Patch method must be static and public.

If the target method is static, the signature of the patch method must match the signature of target method:

```cs
//Target method
private static bool TargetMethod(string text,byte[] array)
{
//...
}

//Patch method
public static bool PatchMethod(string something,byte[] somethingelse)
{
//...
}
```

If the target method is not static, patch method must return the same type, first argument of the patch method must be type target method belongs to and the rest of the arguments are copied from the target method:

```cs
//Target method
class A
{
  private int TargetMethod(string text)
  {
  //...
  }
}

//Patch method
public static int PatchMethod(A instance,string text)
{
//...
}
```

Within the patch method you can modify the return value of the TargetMethod:

```cs
//Target method
private static bool TargetMethod(string text)
{
//...
}

//Patch method
public static bool PatchMethod(string text)
{
  return true;
}
```

You can execute the original method by calling the patch method from within itself, the values of passed parameters will be ignored to change parameters modify the values of parameters passed to patch method:

```cs
//Target method
private static bool TargetMethod(string text)
{
//...
}

//Patch method
public static bool PatchMethod(string text)
{
  //This will change the text parameter in the original method
  text = "Something else";
  //This will return the execution to the original method
  return PatchMethod(text);
}
```

Or you could do both:

```cs
//Target method
private static bool TargetMethod(string text)
{
//...
}

//Patch method
public static bool PatchMethod(string text)
{
  if(text.Contains("Invalid password"))
    return true;
  else
    return PatchMethod(text);
}
```

If the method is not static you can also edit the values of fields and properties on the object associated with target method.

## How to patch assembly

Once you wrote the patch methods and compiled them into a .NET assembly open ACE patcher

Go to file -> open or click ctrl+o or drag n' drop target assembly onto ACE patcher.
Note: if you are loading in a packed assembly you must use 32 bit ACE patcher for 32 bit assembly and 64 bit ACE patcher for 64 bit assembly.
After the assembly is imported you will see it and it's dependancies in the 'Assembly Tree' panel, this panel has an interface similar to that of DnSpy.
Navigate to the methods you want to patch and double click them, if this is the first patch you are making the program will ask you to load the patch assembly.
A new window will appear that will list all the methods in the patch assembly, choose the one that patches the target method and then click on Add patch button.
Repeat that for all the methods you want to patch and after you've patched all the methods click on the apply patches button.
Then, in the result folder, move all of the files associated with the main assembly(resources ,configurations ,dll dependancies , misc files) and then click on Execute.bat to run patched version of the assembly.

## Export patch

Let's say you have 5 assemblies that use the same auth provider, it would be tedious to manually patch every single one, so that is why export functionality exists to use it first patch one of the assemblies manually then go to file -> Export or click ctrl+E and the patch will be saved as file then load in the seconds assembly go to file -> Import or clicl ctrl+I to import all of the patches, these exports can also be distributed.

ACE patcher also allows you to password protect patches to export password protected patch go to File -> Secure Export or click ctrl+shift+E, when you try to import password protected patch you will be asked to provide the password and you won't be able to load the patch without the correct password.

## Credits
* [dnlib](https://github.com/0xd4d/dnlib) - This is used for .net assembly manipulation
* [Harmony](https://github.com/pardeike/Harmony)  - This is used for patching the assemblies
* [JitFreezer](https://github.com/okieeee/JIT-Freezer) - This is used to deal with anti-dump when loading packed files
* [DarkUI](https://github.com/RobinPerris/DarkUI) - This is used for the dark theme
