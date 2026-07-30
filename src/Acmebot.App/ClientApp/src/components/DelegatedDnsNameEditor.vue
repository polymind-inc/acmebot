<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { Plus, Trash2 } from 'lucide-vue-next';

import type { SelectableDnsZone } from '@/api/types';
import { displayDnsName } from '@/utils/certificates';
import { createDelegatedAliasRecordName, createDelegatedCnameInstructions, createManagedDnsName, readDnsNameInput, validateOptionalDnsAlias } from '@/utils/dnsNames';

const props = defineProps<{
  selectedZone: SelectableDnsZone | null;
  dnsNames: string[];
  dnsProviderName: string;
}>();

const emit = defineEmits<{
  'add-dns-name': [dnsName: string, dnsProviderName: string];
  'remove-dns-name': [dnsName: string];
  'alias-change': [dnsAlias: string, message: string];
}>();

const recordName = ref('');
const dnsNameError = ref('');
const aliasRecordName = ref('');

const requestedDnsName = computed(() => readDnsNameInput(recordName.value, 'DNS Name').value || null);
const generatedAliasRecordName = computed(() => createDelegatedAliasRecordName(props.dnsNames));
const customAliasRecordName = computed(() => aliasRecordName.value.trim().replace(/^\.+|\.+$/g, ''));
const effectiveAliasRecordName = computed(() => customAliasRecordName.value || generatedAliasRecordName.value);
const delegatedDnsAlias = computed(() => (props.selectedZone && effectiveAliasRecordName.value ? createManagedDnsName(effectiveAliasRecordName.value, props.selectedZone) : ''));
const aliasError = computed(() => validateOptionalDnsAlias(delegatedDnsAlias.value).message);
const delegatedCnameInstructions = computed(() => createDelegatedCnameInstructions(props.dnsNames, aliasError.value ? '' : delegatedDnsAlias.value));

watch(
  () => props.selectedZone,
  () => {
    dnsNameError.value = '';
  },
);

watch(
  [delegatedDnsAlias, aliasError],
  ([dnsAlias, message]) => {
    emit('alias-change', message ? '' : dnsAlias, message);
  },
  { immediate: true },
);

function clearDnsNameError(): void {
  dnsNameError.value = '';
}

function addDnsName(): void {
  dnsNameError.value = '';

  if (!props.selectedZone) {
    dnsNameError.value = 'Select a DNS alias zone before adding a DNS name.';
    return;
  }

  const dnsNameValidation = readDnsNameInput(recordName.value, 'DNS Name');

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
      for="delegated-record-name"
    >DNS Name</label>
    <div class="compound-input compound-input--plain">
      <input
        id="delegated-record-name"
        v-model="recordName"
        type="text"
        placeholder="www.example.com, *.example.com"
        :disabled="!selectedZone"
        :aria-invalid="dnsNameError ? 'true' : 'false'"
        @keydown.enter.prevent="addDnsName"
        @input="clearDnsNameError"
      >
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
      v-if="requestedDnsName"
      class="form-result"
    >
      <span>Requested DNS name</span>
      <strong>{{ displayDnsName(requestedDnsName) }}</strong>
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
    <div
      v-if="selectedZone"
      class="delegated-alias"
    >
      <div>
        <label
          class="form-label"
          for="delegated-alias-record-name"
        >DNS alias record</label>
        <div class="compound-input compound-input--affixed">
          <span class="compound-input__prefix">_acme-challenge.</span>
          <input
            id="delegated-alias-record-name"
            v-model="aliasRecordName"
            type="text"
            autocomplete="off"
            spellcheck="false"
            :placeholder="generatedAliasRecordName || 'contoso-com'"
            :aria-invalid="aliasError ? 'true' : 'false'"
          >
          <span class="compound-input__suffix">.{{ displayDnsName(selectedZone.name) }}</span>
        </div>
        <p
          v-if="aliasError"
          class="form-error"
        >
          {{ aliasError }}
        </p>
      </div>
      <div
        v-if="delegatedDnsAlias && !aliasError"
        class="form-result"
      >
        <span>DNS alias</span>
        <strong>{{ displayDnsName(delegatedDnsAlias) }}</strong>
      </div>
      <div class="cname-list">
        <div
          v-for="instruction in delegatedCnameInstructions"
          :key="instruction.source"
          class="cname-row"
        >
          <span>{{ displayDnsName(instruction.source) }}</span>
          <strong>{{ displayDnsName(instruction.target) }}</strong>
        </div>
      </div>
    </div>
  </div>
</template>
