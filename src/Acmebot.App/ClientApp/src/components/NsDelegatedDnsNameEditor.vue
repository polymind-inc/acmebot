<script setup lang="ts">
import { computed } from 'vue';
import { Plus, Trash2 } from 'lucide-vue-next';

import type { SelectableDnsZone } from '@/api/types';
import { displayDnsName } from '@/utils/certificates';

const props = defineProps<{
  selectedZone: SelectableDnsZone | null;
  dnsNames: string[];
  dnsProviderName: string;
}>();
const emit = defineEmits<{
  'add-dns-name': [dnsName: string, dnsProviderName: string];
  'remove-dns-name': [dnsName: string];
}>();

const delegatedDomain = computed(() => {
  const zone = props.selectedZone?.name.replace(/\.$/, '') ?? '';
  return zone.toLowerCase().startsWith('_acme-challenge.')
    ? zone.slice('_acme-challenge.'.length)
    : '';
});
const candidates = computed(() => delegatedDomain.value
  ? [delegatedDomain.value, `*.${delegatedDomain.value}`]
  : []);

function hasName(name: string): boolean {
  return props.dnsNames.some((existing) => existing.toLowerCase() === name.toLowerCase());
}
function addName(name: string): void {
  if (props.selectedZone && delegatedDomain.value && !hasName(name)) {
    emit('add-dns-name', name, props.selectedZone.dnsProviderName);
  }
}
</script>

<template>
  <div class="form-section">
    <label class="form-label">Certificate DNS names</label>
    <p
      v-if="!selectedZone"
      class="inline-note"
    >
      Select the NS-delegated challenge zone first.
    </p>
    <p
      v-else-if="!delegatedDomain"
      class="form-error"
    >
      Select a DNS zone named _acme-challenge.&lt;domain&gt;.
    </p>
    <template v-else>
      <p class="inline-note">
        The delegated zone validates the root domain, its wildcard, or both.
      </p>
      <div class="dns-list dns-list--editable">
        <button
          v-for="name in candidates"
          :key="name"
          type="button"
          class="secondary-button"
          :disabled="hasName(name)"
          @click="addName(name)"
        >
          <Plus
            :size="16"
            aria-hidden="true"
          />
          {{ displayDnsName(name) }}
        </button>
      </div>
      <div class="form-result">
        <span>DNS-01 challenge record (zone apex @)</span>
        <strong>{{ displayDnsName(selectedZone.name) }}</strong>
      </div>
      <p class="inline-note">
        Existing TXT records at @ will block issuance to prevent data loss.
        Remove obsolete test values before issuing a certificate.
      </p>
    </template>
    <div class="dns-list dns-list--editable">
      <span
        v-for="dnsName in dnsNames"
        :key="dnsName"
        class="dns-chip dns-chip--removable"
      >
        {{ displayDnsName(dnsName) }}
        <button
          type="button"
          title="Remove DNS name"
          @click="emit('remove-dns-name', dnsName)"
        >
          <Trash2
            :size="13"
            aria-hidden="true"
          />
        </button>
      </span>
    </div>
  </div>
</template>
