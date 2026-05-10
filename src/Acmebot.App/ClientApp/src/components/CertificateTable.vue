<script setup lang="ts">
import { computed, reactive, ref } from 'vue';
import { createColumnHelper, FlexRender, getCoreRowModel, getSortedRowModel, useVueTable, type Row, type SortingState } from '@tanstack/vue-table';
import { ArrowDown, ArrowUp, ArrowUpDown, CirclePlus, Filter, LoaderCircle, RefreshCw, Search, SearchX, ServerCog, SlidersHorizontal, X } from 'lucide-vue-next';

import type { CertificateCategory, CertificateItem, CertificateStatusKind } from '@/api/types';
import {
  displayDnsName,
  formatDate,
  getCategoryLabel,
  getCertificateCategory,
  getCertificateStatus,
  getPrimaryZone,
  isShortLived
} from '@/utils/certificates';

import StatusBadge from './StatusBadge.vue';

const props = defineProps<{
  certificates: CertificateItem[];
  loading: boolean;
  selectedCertificate?: CertificateItem | null;
}>();

const emit = defineEmits<{
  select: [certificate: CertificateItem];
  refresh: [];
  add: [];
}>();

type CategoryFilter = 'all' | CertificateCategory;
type StatusFilter = 'all' | CertificateStatusKind;
type CertificateRow = Row<CertificateItem>;
const noProviderValue = '__none';

const filters = reactive({
  query: '',
  category: 'managed' as CategoryFilter,
  status: 'all' as StatusFilter,
  provider: 'all'
});
const sorting = ref<SortingState>([{ id: 'expiresOn', desc: false }]);

const columnHelper = createColumnHelper<CertificateItem>();
const columns = [
  columnHelper.accessor('name', { header: 'Name' }),
  columnHelper.display({ id: 'dnsNames', header: 'DNS Names', enableSorting: false }),
  columnHelper.accessor((certificate) => getCategoryLabel(getCertificateCategory(certificate)), { id: 'category', header: 'Category' }),
  columnHelper.accessor('expiresOn', { header: 'Expires' }),
  columnHelper.accessor((certificate) => `${certificate.keyType ?? ''} ${certificate.keySize ?? certificate.keyCurveName ?? ''}`, { id: 'key', header: 'Key' }),
  columnHelper.display({ id: 'actions', header: '', enableSorting: false })
];

const categoryOptions: { label: string; value: CategoryFilter }[] = [
  { label: 'All', value: 'all' },
  { label: 'Managed', value: 'managed' },
  { label: 'Other CA', value: 'other-ca' },
  { label: 'Unmanaged', value: 'unmanaged' }
];

const providerOptions = computed(() => {
  const providerNames = new Set<string>();
  let hasNoProvider = false;

  for (const certificate of props.certificates) {
    if (certificate.dnsProviderName) {
      providerNames.add(certificate.dnsProviderName);
    } else {
      hasNoProvider = true;
    }
  }

  const options = Array.from(providerNames)
    .toSorted((left, right) => left.localeCompare(right))
    .map((providerName) => ({ label: providerName, value: providerName }));

  if (hasNoProvider) {
    options.push({ label: 'No provider', value: noProviderValue });
  }

  return options;
});

const hasActiveFilters = computed(
  () => filters.query.trim() !== '' || filters.category !== 'managed' || filters.status !== 'all' || filters.provider !== 'all'
);

const filteredCertificates = computed(() => {
  const normalizedQuery = filters.query.trim().toLowerCase();

  return props.certificates.filter((certificate) => {
    const category = getCertificateCategory(certificate);
    const status = getCertificateStatus(certificate);
    const provider = certificate.dnsProviderName ?? noProviderValue;
    const matchesCategory = filters.category === 'all' || filters.category === category;
    const matchesStatus = filters.status === 'all' || filters.status === status.kind;
    const matchesProvider = filters.provider === 'all' || filters.provider === provider;
    const matchesQuery =
      !normalizedQuery ||
      certificate.name.toLowerCase().includes(normalizedQuery) ||
      certificate.dnsNames.some((dnsName) => displayDnsName(dnsName).toLowerCase().includes(normalizedQuery) || dnsName.toLowerCase().includes(normalizedQuery));

    return matchesCategory && matchesStatus && matchesProvider && matchesQuery;
  });
});

const tableTitle = computed(() => {
  if (filters.category === 'all') {
    return 'Certificates';
  }

  return `${getCategoryLabel(filters.category)} Certificates`;
});

const table = useVueTable({
  get data() {
    return filteredCertificates.value;
  },
  columns,
  state: {
    get sorting() {
      return sorting.value;
    }
  },
  onSortingChange: (updaterOrValue) => {
    sorting.value = typeof updaterOrValue === 'function' ? updaterOrValue(sorting.value) : updaterOrValue;
  },
  getCoreRowModel: getCoreRowModel(),
  getSortedRowModel: getSortedRowModel()
});

const zoneGroups = computed(() => {
  const groups: { zoneName: string; rows: CertificateRow[] }[] = [];
  const groupIndexByZone = new Map<string, number>();

  for (const row of table.getRowModel().rows) {
    const zoneName = getPrimaryZone(row.original);
    const groupIndex = groupIndexByZone.get(zoneName);

    if (groupIndex === undefined) {
      groupIndexByZone.set(zoneName, groups.length);
      groups.push({ zoneName, rows: [row] });
    } else {
      groups[groupIndex].rows.push(row);
    }
  }

  return groups;
});

function clearFilters(): void {
  filters.query = '';
  filters.category = 'managed';
  filters.status = 'all';
  filters.provider = 'all';
}

function getSortIcon(sortDirection: false | 'asc' | 'desc') {
  if (sortDirection === 'asc') {
    return ArrowUp;
  }

  if (sortDirection === 'desc') {
    return ArrowDown;
  }

  return ArrowUpDown;
}

function formatCertificateCount(count: number): string {
  return `${count} ${count === 1 ? 'certificate' : 'certificates'}`;
}
</script>

<template>
  <section class="table-surface" aria-labelledby="certificate-table-heading">
    <div class="table-toolbar">
      <div>
        <h2 id="certificate-table-heading" class="section-heading">{{ tableTitle }}</h2>
        <div class="section-meta">{{ filteredCertificates.length }} of {{ certificates.length }}</div>
      </div>
      <div class="table-toolbar__actions">
        <button class="icon-button" type="button" title="Refresh certificates" :disabled="loading" @click="emit('refresh')">
          <RefreshCw :class="{ 'spin': loading }" :size="17" aria-hidden="true" />
          <span>Refresh</span>
        </button>
        <button class="primary-button" type="button" @click="emit('add')">
          <CirclePlus :size="17" aria-hidden="true" />
          <span>Issue Certificate</span>
        </button>
      </div>
    </div>

    <div class="table-controls">
      <label class="search-field">
        <Search :size="16" aria-hidden="true" />
        <input v-model="filters.query" type="search" placeholder="Search certificates or domains" />
      </label>
      <div class="segmented-control" aria-label="Certificate category">
        <button
          v-for="option in categoryOptions"
          :key="option.value"
          type="button"
          :class="{ 'is-selected': filters.category === option.value }"
          @click="filters.category = option.value"
        >
          {{ option.label }}
        </button>
      </div>
      <label class="select-field">
        <Filter :size="15" aria-hidden="true" />
        <select v-model="filters.status">
          <option value="all">Any status</option>
          <option value="valid">Valid</option>
          <option value="warning">Expiring soon</option>
          <option value="expired">Expired</option>
        </select>
      </label>
      <label class="select-field">
        <ServerCog :size="15" aria-hidden="true" />
        <select v-model="filters.provider" :disabled="providerOptions.length === 0">
          <option value="all">Any provider</option>
          <option v-for="option in providerOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
        </select>
      </label>
      <button v-if="hasActiveFilters" class="icon-button" type="button" @click="clearFilters">
        <X :size="16" aria-hidden="true" />
        <span>Clear</span>
      </button>
    </div>

    <div class="table-wrap">
      <table class="certificate-table">
        <thead>
          <tr v-for="headerGroup in table.getHeaderGroups()" :key="headerGroup.id">
            <th v-for="header in headerGroup.headers" :key="header.id">
              <button
                v-if="!header.isPlaceholder && header.column.getCanSort()"
                class="table-sort-button"
                type="button"
                @click="header.column.getToggleSortingHandler()?.($event)"
              >
                <FlexRender :render="header.column.columnDef.header" :props="header.getContext()" />
                <component :is="getSortIcon(header.column.getIsSorted())" :size="14" aria-hidden="true" />
              </button>
              <FlexRender v-else-if="!header.isPlaceholder" :render="header.column.columnDef.header" :props="header.getContext()" />
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading">
            <td colspan="6">
              <div class="table-empty table-empty--compact">
                <LoaderCircle class="spin" :size="24" aria-hidden="true" />
                <strong>Loading certificates</strong>
              </div>
            </td>
          </tr>
          <tr v-else-if="filteredCertificates.length === 0">
            <td colspan="6">
              <div class="table-empty">
                <SearchX :size="28" aria-hidden="true" />
                <strong>No certificates found</strong>
                <span>{{ hasActiveFilters ? 'No rows match the current filters.' : 'No certificates are available yet.' }}</span>
                <button v-if="hasActiveFilters" class="secondary-button" type="button" @click="clearFilters">Clear filters</button>
                <button v-else class="primary-button" type="button" @click="emit('add')">
                  <CirclePlus :size="17" aria-hidden="true" />
                  <span>Issue Certificate</span>
                </button>
              </div>
            </td>
          </tr>
          <template v-else>
            <template v-for="group in zoneGroups" :key="group.zoneName">
              <tr class="zone-group-row">
                <td colspan="6">
                  <div class="zone-group">
                    <span class="zone-group__name">{{ group.zoneName }}</span>
                    <span class="zone-group__meta">{{ formatCertificateCount(group.rows.length) }}</span>
                  </div>
                </td>
              </tr>
              <tr
                v-for="row in group.rows"
                :key="row.original.id"
                class="certificate-row"
                :class="{ 'is-selected': selectedCertificate?.id === row.original.id }"
                @click="emit('select', row.original)"
              >
                <td>
                  <div class="certificate-name">{{ row.original.name }}</div>
                  <div v-if="isShortLived(row.original)" class="inline-note">Short-lived</div>
                </td>
                <td>
                  <div class="dns-list">
                    <span v-for="dnsName in row.original.dnsNames.slice(0, 3)" :key="dnsName" class="dns-chip">{{ displayDnsName(dnsName) }}</span>
                    <span v-if="row.original.dnsNames.length > 3" class="dns-chip dns-chip--muted">+{{ row.original.dnsNames.length - 3 }}</span>
                  </div>
                </td>
                <td>{{ getCategoryLabel(getCertificateCategory(row.original)) }}</td>
                <td>
                  <StatusBadge :certificate="row.original" />
                  <div class="cell-subtext">{{ formatDate(row.original.expiresOn) }}</div>
                </td>
                <td>
                  <span>{{ row.original.keyType ?? '-' }}</span>
                  <span v-if="row.original.keySize" class="cell-subtext">{{ row.original.keySize }} bit</span>
                  <span v-else-if="row.original.keyCurveName" class="cell-subtext">{{ row.original.keyCurveName }}</span>
                </td>
                <td>
                  <button class="row-action" type="button" title="Open certificate details" @click.stop="emit('select', row.original)">
                    <SlidersHorizontal :size="16" aria-hidden="true" />
                  </button>
                </td>
              </tr>
            </template>
          </template>
        </tbody>
      </table>
    </div>
  </section>
</template>
