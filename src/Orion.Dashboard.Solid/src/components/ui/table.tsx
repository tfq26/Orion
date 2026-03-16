import { splitProps, type Component, type JSX, type ParentComponent } from 'solid-js';

export const Table: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children']);

  return (
    <div class={`relative w-full overflow-visible ${local.class ?? ''}`} {...rest}>
      <table class="w-full caption-bottom text-sm">
        {local.children}
      </table>
    </div>
  );
};

export const TableHeader: ParentComponent<JSX.HTMLAttributes<HTMLTableSectionElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children']);

  return (
    <thead class={`${local.class ?? ''}`} {...rest}>
      {local.children}
    </thead>
  );
};

export const TableBody: ParentComponent<JSX.HTMLAttributes<HTMLTableSectionElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children']);

  return (
    <tbody class={`${local.class ?? ''}`} {...rest}>
      {local.children}
    </tbody>
  );
};

export const TableRow: ParentComponent<JSX.HTMLAttributes<HTMLTableRowElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children']);

  return (
    <tr class={`border-b border-gray-50 transition-colors dark:border-gray-800/50 ${local.class ?? ''}`} {...rest}>
      {local.children}
    </tr>
  );
};

export const TableHead: ParentComponent<JSX.ThHTMLAttributes<HTMLTableCellElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children']);

  return (
    <th
      class={`h-12 px-6 py-4 text-left align-middle font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest text-[10px] [&:has([role=checkbox])]:pr-0 ${local.class ?? ''}`}
      {...rest}
    >
      {local.children}
    </th>
  );
};

export const TableCell: ParentComponent<JSX.TdHTMLAttributes<HTMLTableCellElement>> = (props) => {
  const [local, rest] = splitProps(props, ['class', 'children']);

  return (
    <td class={`px-6 py-4 align-middle [&:has([role=checkbox])]:pr-0 ${local.class ?? ''}`} {...rest}>
      {local.children}
    </td>
  );
};
