# Function call interception

Starting from 1.4.3 dll mods can intercept all orleans call, allowing modders
to change existing code behavior.

# Hook modes

Three hook modes are available: pre, post and replace.

## Pre hook

The pre-hook is called before the actual function. It receives as arguments

- the grain key as a string (so for PlayerGrain that's the player id string-encoded).
- the arguments that will be given to the callee

The pre-hook can modify the content of the objects in arguments.

To abort the call, pre hook can throw an exception.

## Post hook

The post hook is called after the actual backend call is made.

It receives as argument the string-encoded grain key, and the result of the
backend call.

It returns a value of same time to return to the caller.

## Replace hook

The replace hook function entirely replaces the backend call.
Arguments are the IIncomingGrainCallContext and the initial function arguments.

Should you wish to call the implementation backend conditionally, usse
`await context.Invoke()` and `context.Result`. Do *not* call the same function
on the grain interface or it will loop back to your code.

# How to do it

Obtain a IHookCallManager from the service provider given to the mod's Initialize function.

Call it's Register method.

    var handle = isp.GetRequiredService<IHookCallManager>().Register("PlayerGrain.CanCreateConstruct", HookMode.PreCall, this, "CanCreateConstructOverride");

The first argument is the classname.methodname to hook. See the "interfaces/"
directory in the mod-toolkit folder for reference.

The class name is usually the interface name without the 'I' prefix.

The object in argument 3 must have a method name matching argument 4, non overloaded.

The method's signature must match what is detailed above, and return a Task or Task<T>.