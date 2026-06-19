<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { Plus, Trash2 } from 'lucide-vue-next';

import type { SelectableDnsZone } from '@/api/types';
import { displayDnsName } from '@/utils/certificates';
import { createManagedDnsName, readDnsNameInput } from '@/utils/dnsNames';

const props = defineProps<{
  selectedZone: SelectableDnsZone | null;
  dnsNames: string[];
  dnsProviderName: string;
}>();

const emit = defineEmits<{
  'add-dns-name': [dnsName: string, dnsProviderName: string];
  'remove-dns-name': [dnsName: string];
}>();

const recordName = ref('');
const dnsNameError = ref('');

const fullDnsName = computed(() => (props.selectedZone ? createManagedDnsName(recordName.value, props.selectedZone) || null : null));

watch(
  () => props.selectedZone,
  () => {
    dnsNameError.value = '';
  },
);

function clearDnsNameError(): void {
  dnsNameError.value = '';
}

function addDnsName(): void {
  dnsNameError.value = '';

  if (!props.selectedZone) {
    dnsNameError.value = 'Select a DNS zone before adding a DNS name.';
    return;
  }

  if (props.dnsProviderName && props.dnsProviderName !== props.selectedZone.dnsProviderName) {
    dnsNameError.value = 'DNS names in one certificate must use the same DNS provider.';
    return;
  }

  const dnsNameValidation = readDnsNameInput(createManagedDnsName(recordName.value, props.selectedZone), 'DNS Name');

  if (dnsNameValidation.message) {
    dnsNameError.value = dnsNameValidation.message;
    return;
  }

  if (props.dnsNames.some((dnsName) => dnsName.toLowerCase() === dnsNameValidation.value.toLowerCase())) {
    dnsNameError.value = 'This DNS name is already in the certificate.';
    return;
  }

  emit('add-dns-name', dnsNameValidation.value, props.selectedZone.dnsProviderName);
  recordName.value = '';
}
</script>

<template>
  <div class="form-section">
    <label
      class="form-label"
      for="managed-record-name"
    >DNS Name</label>
    <div class="compound-input">
      <input
        id="managed-record-name"
        v-model="recordName"
        type="text"
        placeholder="@, www, api, *"
        :disabled="!selectedZone"
        :aria-invalid="dnsNameError ? 'true' : 'false'"
        @keydown.enter.prevent="addDnsName"
        @input="clearDnsNameError"
      >
      <span class="compound-input__suffix">.{{ selectedZone ? displayDnsName(selectedZone.name) : 'zone' }}</span>
      <button
        class="icon-button"
        type="button"
        :disabled="!selectedZone"
        @click="addDnsName"
      >
        <Plus
          :size="16"
          aria-hidden="true"
        />
        <span>Add</span>
      </button>
    </div>
    <div
      v-if="fullDnsName"
      class="form-result"
    >
      <span>Full DNS name</span>
      <strong>{{ displayDnsName(fullDnsName) }}</strong>
    </div>
    <p
      v-if="dnsNameError"
      class="form-error"
    >
      {{ dnsNameError }}
    </p>
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
