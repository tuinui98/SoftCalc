# Variables and user functions

## Variables

```calc
rate=0.07             => Variable Added
1000*(1+rate)^5       => 1402.5517307
```

- `name=value` stores a value. The value is worked out once, when you
  define it; changing another variable later does not change it.
- Names start with a letter or `_`, then letters, digits or `_`.
  Upper and lower case are the same name.
- Built-in names (`pi`, `e`, `i`, `j`, `answer`, `ans`, `mod`, every
  function) cannot be redefined.
- History shows the line with the values put in, faint on the right:
  `1000*(1+rate)^5` shows `1000*(1+0.07)^5`.

## User functions

```calc
hyp(a, b)=sqrt(a^2+b^2)       => Function Added
hyp(3, 4)                     => 5
```

- `name(p1, p2, ...)=body` defines a function with any number of
  parameters; defining the same name again replaces it.
- The body reads variables when the function is called, so it follows
  later changes.
- A function cannot call itself.
- Functions with one parameter can be drawn in the Graph
  (section *Graph*).
- Functions > User Functions inserts a call to one of yours.

## The Functions and Variables windows

In Advanced View, the **Graph** and **Variables** panels on the left
have **Add** and **Edit** buttons. They open an editor:

![The Functions editor](img/editor-functions.png)

- the list on the left holds your definitions; pick one to edit it;
- fill in **Name**, **Parameters** (functions only) and **Definition**
  or **Value**; the line it will send is shown below;
- **Apply** defines without closing; **OK** defines and closes;
- changing the name renames: the new name is defined and the old one
  deleted.

**Delete** in the panel removes the selected definition at once.

## Keeping definitions

Definitions are saved automatically and come back next time.
File > Save Functions & Variables... writes them to a `.pcf` file you
can keep or share; File > Load Functions & Variables... reads one back.
