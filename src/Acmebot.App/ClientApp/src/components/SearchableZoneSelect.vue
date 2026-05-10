<script setup lang="ts">
import { Check, ChevronDown, Search } from 'lucide-vue-next';
import {
  ComboboxAnchor,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxGroup,
  ComboboxInput,
  ComboboxItem,
  ComboboxItemIndicator,
  ComboboxLabel,
  ComboboxRoot,
  ComboboxTrigger,
  ComboboxViewport
} from 'reka-ui';
import { computed, ref, watch } from 'vue';

import type { DnsZoneGroup, SelectableDnsZone } from '@/api/types';
import { displayDnsName } from '@/utils/certificates';

const props = defineProps<{
  groups: DnsZoneGroup[];
  selected: SelectableDnsZone | null;
  loading?: boolean;
}>();

const emit = defineEmits<{
  'update:selected': [zone: SelectableDnsZone | null];
}>();

interface ZoneOption extends SelectableDnsZone {
  key: string;
  displayName: string;
}

interface GroupedOptions {
  providerName: string;
  options: ZoneOption[];
}

const selectedKey = ref('');

const selectedLabel = computed(() => (props.selected ? displayDnsName(props.selected.name) : ''));

const allOptions = computed<ZoneOption[]>(() =>
  props.groups.flatMap((group) =>
    (group.dnsZones ?? []).map((zone) => ({
      ...zone,
      dnsProviderName: group.dnsProviderName,
      displayName: displayDnsName(zone.name),
      key: `${group.dnsProviderName}:${zone.name}`
    }))
  )
);

const groupedOptions = computed<GroupedOptions[]>(() => {
  const map = new Map<string, ZoneOption[]>();

  for (const option of allOptions.value) {
    const options = map.get(option.dnsProviderName) ?? [];
    options.push(option);
    map.set(option.dnsProviderName, options);
  }

  return Array.from(map, ([providerName, options]) => ({ providerName, options }));
});

watch(
  () => props.selected,
  (selected) => {
    selectedKey.value = selected ? `${selected.dnsProviderName}:${selected.name}` : '';
  },
  { immediate: true }
);

watch(selectedKey, (key) => {
  const option = allOptions.value.find((candidate) => candidate.key === key);

  if (!option) {
    emit('update:selected', null);
    return;
  }

  emit('update:selected', {
    name: option.name,
    dnsProviderName: option.dnsProviderName
  });
});

function displaySelectedValue(value: unknown): string {
  const option = allOptions.value.find((candidate) => candidate.key === value);

  return option?.displayName ?? selectedLabel.value;
}

function clearSelection(): void {
  selectedKey.value = '';
}
</script>

<template>
  <ComboboxRoot v-model="selectedKey" class="combobox" open-on-focus open-on-click reset-search-term-on-select>
    <ComboboxAnchor class="combobox__control">
      <Search class="combobox__search-icon" :size="16" aria-hidden="true" />
      <ComboboxInput
        class="combobox__input"
        :display-value="displaySelectedValue"
        placeholder="Search DNS zone"
      />
      <button v-if="selected" class="combobox__clear" type="button" title="Clear selected zone" @click="clearSelection">
        Clear
      </button>
      <ComboboxTrigger class="combobox__toggle" title="Open DNS zone list">
        <ChevronDown :size="16" aria-hidden="true" />
      </ComboboxTrigger>
    </ComboboxAnchor>

    <ComboboxContent class="combobox__popover" position="popper" :side-offset="8">
      <div v-if="loading" class="combobox__state">Loading DNS zones...</div>
      <template v-else>
        <ComboboxViewport>
          <ComboboxEmpty class="combobox__state">No DNS zones found</ComboboxEmpty>
          <ComboboxGroup v-for="group in groupedOptions" :key="group.providerName" class="combobox__group">
            <ComboboxLabel class="combobox__group-label">{{ group.providerName }}</ComboboxLabel>
            <ComboboxItem
            v-for="option in group.options"
            :key="option.key"
            class="combobox__option"
            :value="option.key"
            :text-value="`${option.displayName} ${option.name} ${option.dnsProviderName}`"
          >
              <span>
                <span class="combobox__option-name">{{ option.displayName }}</span>
                <span class="combobox__option-meta">{{ option.name }}</span>
              </span>
              <ComboboxItemIndicator class="combobox__indicator">
                <Check :size="15" aria-hidden="true" />
              </ComboboxItemIndicator>
            </ComboboxItem>
          </ComboboxGroup>
        </ComboboxViewport>
      </template>
    </ComboboxContent>
  </ComboboxRoot>
</template>
