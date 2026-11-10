# UC1.1: Replication Strategy Decision | Azure-hosted Database

Scope. Job Manager persists a new job in the Job Store and synchronously requests a schedule snapshot write by the Job Orchestrator. Creation returns success only when both writes succeed.

## Database Selection

Azure SQL Database is a good fit because jobs and schedule snapshots have predictable, structured fields—such as job ID, owner, schedule, status, and execution parameters—and the system needs reliable validation and updates. A relational database can enforce constraints and relationships, and transactions can protect writes within each database. Flexible job-specific parameters can still be stored as JSON if needed.

## Replication and Routing Design

In-region availability: Enable Azure SQL zone redundancy in a supported service tier/region. Azure SQL provides platform-managed high availability; zone redundancy protects against an availability-zone failure.

Cross-region recovery: Use an Azure SQL failover group from the primary logical server to a secondary logical server in another EU region, with one geo-secondary for each database. Geo-replication is asynchronous; configure a readable secondary and stable read-only/read-write listener endpoints.

Failover policy: Use customer-managed failover with monitoring and a tested runbook. Microsoft-managed failover has a minimum one-hour grace period and is intended for broad regional outages, so it does not fit a 30-60-minute objective as the only response path.

Read/write split: Route all inserts, updates, deletes, and freshness-critical reads (including immediate read-after-create) to the primary/read-write endpoint. Route eligible read-only queries to the failover-group read-only listener; accept replication lag for these reads.

Two-store workflow: After the Job Store insert, write the Orchestrator snapshot. If that write fails, attempt to delete/undo the Job Store insert and return failure. This is compensation, not an atomic transaction across databases; rollback can also fail. Log correlation IDs and provide reconciliation/retry for partial outcomes. A transactional outbox is a more resilient alternative if the API can later accept a pending state.


## Assumptions and Requirements

| Topic | Details |
|---|---|

| Users and creation rate | 1,000 daily users; 1-5 jobs per user per weekday (09:00-17:00 Europe time). Midpoint: 3,000 jobs/workday. |

| Volume and peak | About 0.1 creations/s averaged across the 8-hour window at midpoint; provisional peak 2/s. Validate peak by load testing. |

| Record size and storage | Planning estimate: 4 KB/job and 4 KB/snapshot. About 1?5 GB raw data/year in each store (midpoint ~3 GB), excluding indexes, backups and replicas. |

| Retention | Jobs remain active for up to one year, then are soft-deleted and retained. Storage therefore grows over time; soft deletion does not reclaim space. |

| Read workload | Not specified for UC1.1. Initially budget up to one ownership/duplicate/validation lookup per creation (roughly the create rate); revise if validation is stateless. |

| Consistency | Geo-secondary reads may be stale. Writes go to the primary. Return success only after Job Store and snapshot writes complete; on snapshot failure, attempt a compensating delete in Job Store. |

| Availability / geography | Target recovery from an outage within 30-60 minutes (RTO; validate end-to-end). Primary: an EU region with zone redundancy; DR/readable secondary: a second EU region. RPO is not specified. |
