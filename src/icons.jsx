// Small stroke icons, 16px, currentColor.
const Svg = ({ children, size = 16, fill = 'none' }) => (
  <svg width={size} height={size} viewBox="0 0 16 16" fill={fill} stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    {children}
  </svg>
)

export const Play = (p) => <Svg {...p} fill="currentColor"><path d="M5 3.5v9l7-4.5z" stroke="none" /></Svg>
export const Pause = (p) => <Svg {...p} fill="currentColor"><rect x="4" y="3.5" width="2.6" height="9" rx="1" stroke="none" /><rect x="9.4" y="3.5" width="2.6" height="9" rx="1" stroke="none" /></Svg>
export const Next = (p) => <Svg {...p}><path d="M4 4l5 4-5 4zM12 4v8" /></Svg>
export const Prev = (p) => <Svg {...p}><path d="M12 4L7 8l5 4zM4 4v8" /></Svg>
export const Gear = (p) => <Svg {...p}><circle cx="8" cy="8" r="2.2" /><path d="M8 1.8v1.6M8 12.6v1.6M14.2 8h-1.6M3.4 8H1.8M12.4 3.6l-1.1 1.1M4.7 11.3l-1.1 1.1M12.4 12.4l-1.1-1.1M4.7 4.7L3.6 3.6" /></Svg>
export const Sound = (p) => <Svg {...p}><path d="M3 6.2h2.2L8.5 3.5v9L5.2 9.8H3z" /><path d="M11 5.5a3.5 3.5 0 010 5M12.8 3.8a6 6 0 010 8.4" /></Svg>
export const Mute = (p) => <Svg {...p}><path d="M3 6.2h2.2L8.5 3.5v9L5.2 9.8H3z" /><path d="M11 6l3.5 4M14.5 6L11 10" /></Svg>
export const Branch = (p) => <Svg {...p}><circle cx="4.5" cy="3.5" r="1.5" /><circle cx="4.5" cy="12.5" r="1.5" /><circle cx="11.5" cy="5.5" r="1.5" /><path d="M4.5 5v6M11.5 7c0 3-7 2-7 4" /></Svg>
export const Check = (p) => <Svg {...p}><path d="M3.5 8.5l3 3 6-7" /></Svg>
export const Back = (p) => <Svg {...p}><path d="M10 3.5L5.5 8l4.5 4.5" /></Svg>
export const Terminal = (p) => <Svg {...p}><rect x="1.8" y="2.8" width="12.4" height="10.4" rx="2" /><path d="M4.5 6.5l2 1.5-2 1.5M8.5 10h3" /></Svg>
export const Chip = (p) => <Svg {...p}><rect x="4" y="4" width="8" height="8" rx="1.5" /><path d="M6 1.8V4M10 1.8V4M6 12v2.2M10 12v2.2M1.8 6H4M1.8 10H4M12 6h2.2M12 10h2.2" /></Svg>
export const Spark = (p) => <Svg {...p}><path d="M8 1.5l1.6 4.9 4.9 1.6-4.9 1.6L8 14.5l-1.6-4.9L1.5 8l4.9-1.6z" /></Svg>
export const Timer = (p) => <Svg {...p}><circle cx="8" cy="9" r="5.2" /><path d="M8 9V6.3M6.5 1.8h3" /></Svg>
export const Music = (p) => <Svg {...p}><path d="M6 12.2V3.8l7-1.6v8.4" /><circle cx="4.4" cy="12.2" r="1.7" /><circle cx="11.4" cy="10.6" r="1.7" /></Svg>
