// TERMS
// Task: same as Promise in TS
// const: a value that gets inlined at compile time,
//        and therefore requires app restart when changed
// LINQ: Language Integrated Query, allows chaining
//       or sql like syntax against any IEnumerable
// sealed: a class that can't be inherited from
// using: when preceding a call to an object instantiation
//        which implements IDisposable, automatically disposes it.
//        can be async or sync disposal, if async can be awaited
//
// out: a variable declared as a parameter passed by reference
//      to a synchronous function. The function must assign
//      the value
// background service: a .NET way to run schedulable code
// record: a value object, equality compared by equal fields instead of object id
// [NotNullWhen(true)] out MyType? myParameter: attribute telling compiler that if the
// routine returns true then the out parameter will be not null
// is: means x matches shape of y even if y contains additional stuff
// delegate: keyword - a type declaration for a function
//
//
// UNCERTAIN:
// - "register" something as "scoped"
// meaning "one isntance per scope"
// - hosted service
//
// OrphanedImageSweeper test questions
// - AddUploadToDisk may drift from how we upload
// files, can we re-use the real upload handler method?
