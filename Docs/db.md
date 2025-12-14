# **Database Schema Specification: SovereignTask**

Version: 1.0  
Date: October 26, 2023  
Architectural Pattern: Multi-Tenant Normalized Relational Model

### **1\. High-Level Overview**

This database architecture is designed to support "SovereignTask," a high-performance, security-focused task management platform. It utilizes a **Multi-Tenant, Normalized Relational Model** optimized for data sovereignty and strict isolation.

* **Tenancy Model:** A tenants table acts as the root isolation layer. All downstream entities (projects, tasks, users) are scoped by tenant\_id to ensure strict data sovereignty.  
* **Concurrency Control:** To support "Optimistic UI" and offline sync as required by FR-01 and Edge Cases, critical tables include row\_version tokens to handle merge conflicts.  
* **Hierarchical Data:** The tasks table implements the **Adjacency List** pattern (parent\_id) to support the infinite nesting requirement (FR-05) via recursive CTEs.  
* **Auditability:** A robust audit\_logs table captures all state changes to satisfy the "Compliance Officer" persona.

### **2\. Entity Relationship Summary**

The following table summarizes the core relationships between entities in the system.

| Source Entity | Relationship Type | Target Entity | Cardinality | Description/Logic |
| :---- | :---- | :---- | :---- | :---- |
| Tenants | has many | Users | 1:N | A workspace/tenant contains multiple users. |
| Tenants | has many | Projects | 1:N | Workspaces contain multiple projects. |
| Projects | has many | Tasks | 1:N | Projects act as the container for tasks. |
| Tasks | has many | Tasks (Subtasks) | 1:N | Self-referential relationship for infinite nesting (FR-05). |
| Tasks | has many | TaskDependencies | 1:N | Defines Gantt chart relationships (e.g., Task A blocks Task B). |
| Tasks | has many | TimeEntries | 1:N | Time tracking logs associated with specific tasks. |
| Users | has many | TimeEntries | 1:N | Users log time against tasks. |
| Projects | has many | MagicLinks | 1:N | Secure, temporary access links for external clients (FR-03). |
| Tasks | has many | AuditLogs | 1:N | Granular history of changes (Who, What, When). |

### **3\. Detailed Schema Specification**

#### **Table: tenants**

**Description:** The root entity representing a workspace or organization, ensuring data isolation.

| Column Name | Data Type | Constraints | References | Description |
| :---- | :---- | :---- | :---- | :---- |
| id | UUID | PK, Not Null | \- | Unique Tenant ID |
| name | VARCHAR(255) | Not Null | \- | Organization name |
| subscription\_status | VARCHAR(50) | Not Null | \- | e.g., 'active', 'trial', 'past\_due' |
| created\_at | TIMESTAMP | Not Null | \- | Record creation time |
| updated\_at | TIMESTAMP | Not Null | \- | Last update time |

#### **Table: users**

**Description:** Stores user identity information, linking local profiles to external Identity Providers (Entra ID).

| Column Name | Data Type | Constraints | References | Description |
| :---- | :---- | :---- | :---- | :---- |
| id | UUID | PK, Not Null | \- | Unique User ID |
| tenant\_id | UUID | FK, Not Null | tenants.id | The workspace this user belongs to |
| entra\_oid | VARCHAR(255) | Unique, Nullable | \- | Object ID from Microsoft Entra ID (for SSO) |
| email | VARCHAR(255) | Unique, Not Null | \- | User email address |
| display\_name | VARCHAR(100) | Not Null | \- | Full name for display on boards |
| role | VARCHAR(50) | Not Null | \- | RBAC Role (e.g., 'admin', 'member', 'guest') |
| created\_at | TIMESTAMP | Not Null | \- | \- |
| updated\_at | TIMESTAMP | Not Null | \- | \- |

#### **Table: projects**

**Description:** High-level containers for tasks, typically representing a client engagement or internal initiative.

| Column Name | Data Type | Constraints | References | Description |
| :---- | :---- | :---- | :---- | :---- |
| id | UUID | PK, Not Null | \- | Unique Project ID |
| tenant\_id | UUID | FK, Not Null | tenants.id | Tenant isolation |
| name | VARCHAR(255) | Not Null | \- | Project Name |
| key\_prefix | VARCHAR(10) | Not Null | \- | Short code for task IDs (e.g., "WEB" for WEB-101) |
| description | TEXT | Nullable | \- | Project details |
| is\_archived | BOOLEAN | Default False | \- | Soft delete/archive flag |
| created\_at | TIMESTAMP | Not Null | \- | \- |
| updated\_at | TIMESTAMP | Not Null | \- | \- |

#### **Table: task\_statuses**

**Description:** Configurable workflow columns (Kanban columns) per project or tenant.

| Column Name | Data Type | Constraints | References | Description |
| :---- | :---- | :---- | :---- | :---- |
| id | UUID | PK, Not Null | \- | Unique Status ID |
| project\_id | UUID | FK, Not Null | projects.id | Scopes status to a project |
| name | VARCHAR(50) | Not Null | \- | e.g., "To Do", "In Progress", "QA" |
| position | INT | Not Null | \- | Ordering on the Kanban board (0, 1, 2...) |
| is\_completed | BOOLEAN | Default False | \- | Flags if this status represents "Done" |

#### **Table: tasks**

**Description:** The core entity. Uses an adjacency list pattern for infinite subtask nesting and includes fields for Gantt charting.

| Column Name | Data Type | Constraints | References | Description |
| :---- | :---- | :---- | :---- | :---- |
| id | UUID | PK, Not Null | \- | Unique Task ID |
| project\_id | UUID | FK, Not Null | projects.id | Parent project |
| parent\_id | UUID | FK, Nullable | tasks.id | Recursive self-reference for subtasks |
| status\_id | UUID | FK, Not Null | task\_statuses.id | Current column on Kanban board |
| assignee\_id | UUID | FK, Nullable | users.id | User responsible for the task |
| title | VARCHAR(255) | Not Null | \- | Task summary |
| description | TEXT | Nullable | \- | Rich text details |
| priority | INT | Default 0 | \- | 0=Low, 1=Medium, 2=High, 3=Critical |
| start\_date | TIMESTAMP | Nullable | \- | Gantt chart start |
| due\_date | TIMESTAMP | Nullable | \- | Gantt chart end/deadline |
| estimated\_minutes | INT | Default 0 | \- | Estimation for planning |
| row\_version | INT | Default 1 | \- | Optimistic concurrency token (FR-01/Edge Cases) |
| created\_at | TIMESTAMP | Not Null | \- | \- |
| updated\_at | TIMESTAMP | Not Null | \- | \- |

#### **Table: task\_dependencies**

**Description:** Defines Finish-to-Start (FS) relationships between tasks to power the "Interactive Gantt Chart" critical path logic (FR-02).

| Column Name | Data Type | Constraints | References | Description |
| :---- | :---- | :---- | :---- | :---- |
| id | UUID | PK, Not Null | \- | Unique Dependency ID |
| predecessor\_id | UUID | FK, Not Null | tasks.id | The task that must finish first |
| successor\_id | UUID | FK, Not Null | tasks.id | The task that cannot start until predecessor finishes |
| type | VARCHAR(10) | Default 'FS' | \- | Dependency type (Finish-to-Start) |

#### **Table: time\_entries**

**Description:** High-volume table for the "Native Time Tracking" requirement (FR-04).

| Column Name | Data Type | Constraints | References | Description |
| :---- | :---- | :---- | :---- | :---- |
| id | UUID | PK, Not Null | \- | Unique Entry ID |
| task\_id | UUID | FK, Not Null | tasks.id | Linked task |
| user\_id | UUID | FK, Not Null | users.id | Who tracked the time |
| start\_time | TIMESTAMP | Not Null | \- | Timer start |
| end\_time | TIMESTAMP | Nullable | \- | Timer stop (NULL \= currently running) |
| description | VARCHAR(255) | Nullable | \- | Optional notes on work done |

#### **Table: magic\_links**

**Description:** Secure tokens for the "Client Portal" allowing read-only or comment-only access without full accounts (FR-03).

| Column Name | Data Type | Constraints | References | Description |
| :---- | :---- | :---- | :---- | :---- |
| id | UUID | PK, Not Null | \- | Unique Link ID |
| project\_id | UUID | FK, Not Null | projects.id | The project shared via this link |
| token | VARCHAR(64) | Unique, Not Null | \- | Cryptographically secure token |
| access\_level | VARCHAR(20) | Not Null | \- | 'read\_only' or 'comment\_only' |
| expires\_at | TIMESTAMP | Not Null | \- | Time-bound expiration |
| created\_by | UUID | FK, Not Null | users.id | User who generated the link |

#### **Table: audit\_logs**

**Description:** Immutable ledger for compliance, tracking every state change ("Who, What, When").

| Column Name | Data Type | Constraints | References | Description |
| :---- | :---- | :---- | :---- | :---- |
| id | BIGINT | PK, Auto Inc | \- | Sequential ID for sorting |
| tenant\_id | UUID | FK, Not Null | tenants.id | Scoping |
| entity\_table | VARCHAR(50) | Not Null | \- | e.g., 'tasks', 'projects' |
| entity\_id | UUID | Not Null | \- | ID of the modified record |
| action | VARCHAR(20) | Not Null | \- | 'CREATE', 'UPDATE', 'DELETE' |
| changed\_by | UUID | FK, Not Null | users.id | User responsible |
| changes\_json | JSON/TEXT | Nullable | \- | Snapshot of OldValue/NewValue |
| created\_at | TIMESTAMP | Not Null | \- | When the change occurred |

### **4\. Constraints & Indexes**

#### **Performance Indexes**

* **Kanban Board Optimization:**  
  * CREATE INDEX idx\_tasks\_project\_status ON tasks (project\_id, status\_id);  
  * *Reason:* Rapidly loads the board view by filtering tasks per project and grouping by status (FR-01).  
* **Recursive Tree Traversal:**  
  * CREATE INDEX idx\_tasks\_parent ON tasks (parent\_id);  
  * *Reason:* Optimizes the recursive CTE query required to build the task hierarchy (FR-05).  
* **Gantt Chart Dependencies:**  
  * CREATE INDEX idx\_deps\_predecessor ON task\_dependencies (predecessor\_id);  
  * *Reason:* Quickly finds all downstream tasks affected when a parent task is moved (FR-02).  
* **Running Timers:**  
  * CREATE INDEX idx\_time\_entries\_active ON time\_entries (user\_id) WHERE end\_time IS NULL;  
  * *Reason:* Instant access to the "current running timer" for the UI header.

#### **Data Integrity Constraints**

* **Circular Dependency Prevention:** Application logic must prevent a task from being its own ancestor to avoid infinite loops in CTEs.  
* **Time Logic:** CHECK (end\_time \> start\_time) on time\_entries to prevent negative duration.  
* **Gantt Logic:** CHECK (due\_date \>= start\_date) on tasks to ensure logical scheduling.  
* **Unique Magic Tokens:** UNIQUE (token) on magic\_links is critical for security.  
* **Entra ID Mapping:** UNIQUE (tenant\_id, entra\_oid) ensures a specific Entra ID user is mapped only once within a specific tenant.