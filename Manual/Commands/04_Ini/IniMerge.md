# IniMerge

This command will combine the contents of two .ini files.

## Syntax

```pebakery
IniMerge,<SrcFile>,<DestFile>
```

### Arguments

| Argument | Description |
| --- | --- |
| SrcFile | The full path of the file containing the new information. |
| DestFile | The full path of the file to be updated with the new information. |

## Remarks

+ `SrcFile` will never be modified.
+ If `DestFile` does not exist it will be created.
+ Only .ini specific contents will be merged. Anthing that is not a [Section] name or part of a key=value pair will be ignored. Standard .ini recognized comments such as `; Comment` or `# Comment` as well as PEBakery style comments `// Comment` will likewise be ignored.

## Example

Lets assume we have the following .ini files.

C:\myFile1.ini:

```pebakery
[Variables]
// Here are some variables
myvar1="Homes32"
myvar2="ChrisR"

[Interface]
pText1="Show this text:",1,1,10,8,106,18,8,Normal
pCheckbox1="Show Window?",1,3,10,32,278,18,False

[Interface-2]
pText1="Show other text:",1,1,10,8,106,18,8,Normal
```

C:\myFile2.ini:

```pebakery
[Variables]
myvar1="Homes32"
myvar2="lancelot"

[Interface]
pText1="Write something here:",1,1,10,8,106,18,8,Normal
pCheckbox1="Show Window?",1,3,10,32,278,18,False
```

In the following example the file `SrcFile` *C:\myFile1.ini* will be merged into `DestFile` *C:\myFile2.ini*

```pebakery
IniMerge,C:\myFile1.ini,C:\myFile2.ini
```

The resulting C:\myFile2.ini file:

```pebakery
[Variables]
myvar1="Homes32"
myvar2="ChrisR"

[Interface]
pText1="Show this text:",1,1,10,8,106,18,8,Normal
pCheckbox1="Show Window?",1,3,10,32,278,18,False

[Interface-2]
pText1="Show other text:",1,1,10,8,106,18,8,Normal
```

As we can see several things have happened.

+ The comment `// Here are some variables` was not merged because it is not a valid .ini file component.
+ The key `myvar2` was updated with the value from the `SrcFile`.
+ The label for the element *pText1* was changed.
+ The section `Interface-2` did not exist in the `DestFile` so it was created.
+ `myvar1` and `pCheckbox1` were identical in both files so they were not modified.
