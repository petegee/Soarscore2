# High Level Architecture

## Core Principles
 - System is headless only
 - Hexagonal architecture
 - SOLID
 - Domain Driven Design
 - Lean and Focused
 - Testable by design
 - System assumes no specific external UX requirements
 - Access is strictly via an REST based API
 - The API interaction is "intent-based" 
 - Append only immutable log as state storage (Event Sourced)
 - CQRS pattern to cleanly seperate Reads from Writes
 - Commands and Queries only
 - Functional-Like as a Core Princple

 ### Headless
The system must be headless in nature. It must not offer a UI or alternative interaction.

### Hexagonal architecture
At its simplest form, the application should be onion layered with dependencies pointing inwards only.
The layers should be:
 1, API: an http adapter, 
 2, Application: exposes Ports, contains domain services, and use cases, state management
 3, Domain: Contains the aggregate roots, and their entities/value objects

### SOLID
It should try to maintain SOLID principes, but we care more about (SLID): 
 - Single Responsibilty - Single reason to change.
 - Liskov Substitution Principle - Subtypes must be substitutable for their base types without altering the correctness of the program.
 - Interface Segregation Principle - Clients should not be forced to depend on methods they do not use.
 - Dependency Inversion Principle - "Dont ask, we'll tell you"

### Domain Driven Design
Domain-Driven Design (DDD) is a software development approach that aligns code structure with 
real business needs by modeling software around a specific domain using a shared vocabulary. The 
Domain layer must have **NO** dependencies.

### Lean and Focused
This system is to be as light weight and as un-opinionated as possible to allow maximum 
integrator flexibility while covering no more than the core domain.

### Testable by design
All functionality must be verifiable by test which are:
 - fast, 
 - independent
 - reliable 
 - automated 
 - isolated - Tests must not depend on each other
 - Be able to be driven without HTTP testing tools
 - NO UI TESTING

 Tests must not have unessessary knowledge of the structure of the code they're testing. 
 Eg Black-Box sociable style tests.

#### Overlapping Socialable Testing is preferred
We prefer to minimise solitary style white-box testing and mocking, and instead choose to 
do overlapping sociable black-box style tests verifing behaviour without "double-encoding"
the structure and dependencies of the application.

### System assumes no specific external UX requirements
We do not let external parties shape this system. It is the core kernel of a wider integrated 
competition system (which may have displays, sounds, announcements, score gathering systems etc).

### Access is strictly via an REST based API
The one and only adapter provided by this solution is an HTTP RESTfull API.

### The API interaction is "intent-based" 
We must only use "intent-based" interactions over HTTP. This means
 - only accept POSTs (a Command), and GETs (a Query)
 - POSTs paths are to verbs not nouns - to decouple consumers from the domain structures. 
 - There are NEVER any PUTs, PATCHES, DELETES, or OPTIONS
 - the new HTTP QUERY will not be supported just yet. 
 - Queries must send parameters on the query string - NEVER in the body.

### Append only immutable log as state storage (Event Sourced)
 - The system is Event-Sourced. 
 - Events are the state. 
 - Events are immutable. 
 - Read-model projections are a necessary evil, but should only be used when doing cross-stream 
   queries or query by something other than an aggregate root's ID
 - If querying by ID, then you must use load the stream.

### CQRS pattern to cleanly seperate Reads from Writes
The mechanisms to do queries must be separate from the mechanism which does state changes.

### Commands and Queries only
The application layer only supports either Commands or Queries. These are dispatched via a 
mediator pattern. This dove-tails nicely with intent-based APIs.

### Functional-Like as a Core Princple
The system code should borrow core concepts of functional programming (but not necessarily 
use a functional language). The principles we choose to apply are:
 - Immutability. Data is immutable, meaning once a value is assigned, it cannot be changed.
 - Pure functions where possible. A pure function always produces the same output for the 
   same input. It does not have side effects.
 - Function Composition. Functions can be combined to build more complex functions.
 - Declarative Style. The focus is on what to solve rather than how to solve it.
 - Avoiding Side Effects. Functions should not produce side effects that affect 
   the program's state outside their scope
