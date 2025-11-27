# Load Test Checklist

A copyable checklist for conducting performance and load testing of Elsa Workflows deployments. Use this to ensure comprehensive coverage of test scenarios.

---

## Pre-Test Preparation

### Environment Baseline

- [ ] Document production-equivalent infrastructure specs
  - [ ] CPU cores and type
  - [ ] Memory allocation
  - [ ] Storage type (SSD/HDD) and IOPS
  - [ ] Network latency between components
- [ ] Record current database size and table row counts
- [ ] Capture baseline metrics (idle state):
  - [ ] CPU utilization
  - [ ] Memory usage
  - [ ] Database connection count
  - [ ] Lock provider connection count
- [ ] Verify monitoring and tracing are enabled
- [ ] Confirm log aggregation is capturing all nodes
- [ ] Disable or isolate non-test traffic

### Synthetic Workflow Preparation

- [ ] Create workflows with **no external I/O**:
  - [ ] Simple sequence (5-10 activities)
  - [ ] Branching/decision workflow
  - [ ] Parallel execution (fan-out/fan-in)
  - [ ] Long sequence (50+ activities)
- [ ] Verify workflows execute correctly in isolation
- [ ] Document expected execution time per workflow type
- [ ] Prepare workflow definitions in deployable format (JSON/code)

---

## Test Execution

### Phase 1: Baseline (Synthetic Workflows)

- [ ] Execute single workflow, record timing
- [ ] Execute 10 concurrent workflows, record:
  - [ ] Total duration
  - [ ] P50/P95/P99 latency
  - [ ] Error count
- [ ] Execute 100 concurrent workflows, record same metrics
- [ ] Identify CPU/memory utilization at each level
- [ ] **Expected result:** Linear scaling up to CPU saturation

### Phase 2: Incremental Complexity

- [ ] Add database read operations to workflow
  - [ ] Record latency impact
- [ ] Add database write operations
  - [ ] Record latency and DB load impact
- [ ] Add HTTP calls to mock service
  - [ ] Record latency with various mock response times
- [ ] Add parallel branches (2, 4, 8, 16)
  - [ ] Record optimal branch count
- [ ] **Expected result:** Identify bottleneck transitions

### Phase 3: Ramp Test

- [ ] Start with 10 concurrent users/workflows
- [ ] Increase by 10 every minute
- [ ] Continue until one of:
  - [ ] Error rate exceeds 1%
  - [ ] P99 latency exceeds SLA
  - [ ] CPU exceeds 80%
  - [ ] Memory exceeds 80%
- [ ] Record maximum sustainable throughput
- [ ] Document bottleneck (CPU, memory, DB, locks)

### Phase 4: Soak Test

- [ ] Run at 70% of maximum sustainable load
- [ ] Duration: minimum 4 hours (8+ hours preferred)
- [ ] Monitor for:
  - [ ] Memory leaks (increasing memory over time)
  - [ ] Connection pool exhaustion
  - [ ] Lock accumulation
  - [ ] Database growth rate
  - [ ] Error rate stability
- [ ] **Expected result:** Stable metrics throughout

### Phase 5: Spike Test

- [ ] Establish baseline at 50% load
- [ ] Spike to 150% of maximum sustainable load
- [ ] Maintain spike for 5 minutes
- [ ] Return to baseline
- [ ] Monitor:
  - [ ] Recovery time to baseline metrics
  - [ ] Error behavior during spike
  - [ ] Queue depth growth and drain
  - [ ] Lock acquisition delays
- [ ] **Expected result:** Graceful degradation, full recovery

---

## Metrics to Capture

### Workflow Execution Metrics

| Metric | P50 | P95 | P99 | Max | Notes |
|--------|-----|-----|-----|-----|-------|
| Workflow run duration | | | | | |
| Activity execution duration | | | | | |
| Bookmark creation time | | | | | |
| Bookmark resume time | | | | | |

### System Metrics

| Metric | Baseline | Under Load | Peak | Notes |
|--------|----------|------------|------|-------|
| CPU utilization (%) | | | | |
| Memory utilization (%) | | | | |
| Active DB connections | | | | |
| DB query latency (ms) | | | | |
| Lock acquisition time (ms) | | | | |

### Throughput Metrics

| Metric | Value | Notes |
|--------|-------|-------|
| Workflows started/min | | |
| Workflows completed/min | | |
| Bookmarks created/min | | |
| Bookmarks resumed/min | | |
| Error rate (%) | | |

---

## Regression Comparison

### Compare Against Previous Baseline

- [ ] Load previous baseline results
- [ ] Calculate percentage difference for each metric
- [ ] Flag any metric with > 10% regression
- [ ] Document acceptable regressions with justification

### Regression Threshold Table

| Metric | Previous | Current | Delta (%) | Status |
|--------|----------|---------|-----------|--------|
| Max sustainable throughput | | | | ✅/⚠️/❌ |
| P99 latency at 50% load | | | | ✅/⚠️/❌ |
| P99 latency at 80% load | | | | ✅/⚠️/❌ |
| Memory at 80% load | | | | ✅/⚠️/❌ |
| Error rate at 80% load | | | | ✅/⚠️/❌ |

**Status Key:**
- ✅ Within 5% of baseline
- ⚠️ 5-10% regression (investigate)
- ❌ > 10% regression (blocking)

---

## Rollback Criteria

Define clear rollback triggers before deploying performance-sensitive changes:

### Immediate Rollback

- [ ] Error rate exceeds **5%** for > 1 minute
- [ ] P99 latency exceeds **2x baseline** for > 2 minutes
- [ ] Any node becomes unresponsive
- [ ] Database connection pool exhausted

### Monitored Rollback

- [ ] Error rate between 1-5% sustained > 5 minutes
- [ ] P99 latency between 1.5x-2x baseline > 10 minutes
- [ ] Memory growth > 10% per hour
- [ ] Lock acquisition timeouts > 1% of attempts

---

## Post-Test Actions

### Documentation

- [ ] Record all metrics in test report
- [ ] Document any anomalies observed
- [ ] Capture relevant logs and traces
- [ ] Screenshot dashboards at peak load
- [ ] Update baseline for future comparisons

### Environment Cleanup

- [ ] Clear test workflow instances
- [ ] Reset database statistics
- [ ] Verify production traffic routing restored
- [ ] Archive test artifacts

### Recommendations

- [ ] Identify optimization opportunities
- [ ] Document scaling thresholds for auto-scaling rules
- [ ] Update capacity planning models
- [ ] Schedule follow-up tests if needed

---

## Quick Reference Commands

```bash
# Generate synthetic load (example using curl)
for i in {1..100}; do
  curl -X POST http://elsa-server/elsa/api/workflow-instances \
    -H "Content-Type: application/json" \
    -d '{"definitionId": "LoadTestWorkflow"}' &
done
wait

# Monitor active workflow count
watch -n 1 'curl -s http://elsa-server/elsa/api/workflow-instances?status=Running | jq ".totalCount"'

# Check database connections (PostgreSQL)
psql -c "SELECT count(*) FROM pg_stat_activity WHERE datname = 'elsa';"

# Monitor lock acquisitions in logs
kubectl logs -l app=elsa-server --tail=1000 | grep -c "Acquired distributed lock"
```

---

## Related Documentation

- [Performance & Scaling Guide](../README.md) - Optimization strategies
- [Throughput Tuning Examples](throughput-tuning.md) - Configuration snippets
- [Clustering Guide](../../clustering/README.md) - Multi-node setup

---

**Last Updated:** 2025-11-27
