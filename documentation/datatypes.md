![Documentation Image](images/documentation_header.png)

## Data types in moddesc.ini
There are several data types you will need to know about for moddesc.ini files.

### Headers and Descriptors
Mod Manager mods are defined by their moddesc.ini files which are located in the mod folder. The folder, mod files and this moddesc.ini file make a Mod Manager mod. The moddesc.ini format has 2 key terms: **Headers** and **Descriptors**. 

#### Headers
Headers are items encased in square brackets like `[ModManager]` and `[CUSTOMDLC]`. They do not contain spaces. Headers are case sensitive.
```
[ModInfo]
...

[CUSTOMDLC]
...
```

#### Descriptors
Underneath headers are descriptors, which describe items for most recent header above it. Descriptors are key/value pairs. Descriptor keys are case sensitive.
```
key=value
sourcedirs=DLC_MOD_UIScaling
``` 

Descriptor spacing does not matter. The values before the `=` and after it are trimmed of outer whitespace. The following all parse to equivalent items:
```
descriptor = value
descriptor=value
descriptor   =    value
```


### Structs
Some descriptors use structs, which are modeled after how BioWare's Coalesced.ini and decompiled Coalesced.bin files are. A struct is a list of keys mapped to values. The keys are always unquoted and will never contain spaces. The values may be quoted or unquoted. M3 supports both - ME3CMM has a relatively weak implementation of a struct parser and may or may not work in some instances.

```
structexample=(Key1=Value1,Key2="Value 2")
```

Any value that contains a space MUST be quoted. All key/value pairs must be separated by a comma. Text inside of "quotes" will not trigger the special characters , ) or (. You cannot include the " symbol in your strings as this is a reserved character for parsing. If you want to simulate "quoting" something, use 'single quotes'.

### Struct lists
Some descriptors use a list of structs. Lists are formed by an opening and closing parenthesis, with each struct separated by a comma. **This is an additional set of parenthesis! Structs have their own enclosing parenthesis.** However, a one item struct list does not have to be surrounded by an additional set of parenthesis. You can choose to leave them on or off for one item lists.

Some examples:

```
twoitemlist=((X=1, Y=1),(X=2,X=3))
oneitemlist=((text="hello there", speaker=obiwan))
anotheroneitemlist=(text="GENERAL KENOBI!", speaker=GeneralGreivous)
```

### String lists
Some descriptors take a list of strings. These are values separated by a `;`. 
```
outdatedcustomdlc=DLC_MOD_OldMod1;DLC_MOD_OldMod2;DLC_MOD_OldMod3
```

### Comments
Comments are lines that being with `;`. They are essentially ignored. Note that if a line is setup like requireddlc=DLC_MOD_EGM;DLC_MOD_EGM_Squad, this is not a comment. Only ; at the start of a line is considered a comment. You cannot put a ; on the end of another line for comments, if you wish to add comments, you should ensure they are on their own lines.

### Value types
Some descriptors use value types (sometimes called data types). They are pretty much the same across sane programming langauges.

 - **Integer**: 1, 2, 3, 0
 - **Float**: 1.0, 2.1, 3.333
 - **String**: _Hello_, _I am a string_, _Yes I sure am!_
 - **Quoted string**: "_Hello_", "_I am a quoted string_", "_I am surrounded by quotations!_"
