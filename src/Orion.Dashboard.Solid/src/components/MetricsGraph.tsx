import { Component, For } from 'solid-js';

interface MetricsGraphProps {
  data: { cpu: number; memory: number }[];
  type: 'cpu' | 'memory';
  height?: number;
}

export const MetricsGraph: Component<MetricsGraphProps> = (props) => {
  const h = props.height || 120;
  const w = 400;
  const max = props.type === 'cpu' ? 100 : 1000;
  
  const points = () => {
    if (!props.data.length) return "";
    return props.data.map((d, i) => {
      const x = (i / (props.data.length - 1)) * w;
      const val = props.type === 'cpu' ? d.cpu : d.memory;
      const y = h - (val / max) * h;
      return `${x},${y}`;
    }).join(" ");
  };

  const areaPoints = () => {
    const p = points();
    if (!p) return "";
    return `${p} ${w},${h} 0,${h}`;
  };

  return (
    <div class="relative w-full h-full bg-black/40 border border-white/5 p-4 group overflow-hidden">
      <div class="absolute top-2 left-4 text-[8px] font-mono text-gray-600 uppercase tracking-widest z-10">
        {props.type} Load Vector
      </div>
      
      <svg viewBox={`0 0 ${w} ${h}`} class="w-full h-full overflow-visible">
        <defs>
          <linearGradient id={`grad-${props.type}`} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color={props.type === 'cpu' ? '#00E6F3' : '#BC6FF1'} stop-opacity="0.3" />
            <stop offset="100%" stop-color="transparent" />
          </linearGradient>
        </defs>
        
        {/* Grid lines */}
        <For each={[0, 0.25, 0.5, 0.75, 1]}>
          {(tick) => (
            <line 
              x1="0" y1={h * tick} x2={w} y2={h * tick} 
              stroke="white" stroke-width="0.5" stroke-opacity="0.05" 
            />
          )}
        </For>

        {/* Path */}
        <polyline
          fill="none"
          stroke={props.type === 'cpu' ? '#00E6F3' : '#BC6FF1'}
          stroke-width="2"
          points={points()}
          class="drop-shadow-[0_0_8px_rgba(0,230,243,0.5)]"
        />
        
        {/* Fill */}
        <polygon
          points={areaPoints()}
          fill={`url(#grad-${props.type})`}
        />
      </svg>

      <div class="absolute bottom-2 right-4 text-[10px] font-black text-white">
        {Math.round(props.data[props.data.length - 1]?.[props.type] || 0)}
        <span class="text-[8px] ml-1 text-gray-500">{props.type === 'cpu' ? '%' : 'MB'}</span>
      </div>
    </div>
  );
};
